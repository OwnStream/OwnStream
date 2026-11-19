using System.IO.Compression;
using System.Text.Json;
using OwnStream.PluginCore;
using OwnStream.PluginCore.ResultTypes;

namespace OwnStream.Plugin;

public class PluginManager(ILogger<PluginManager> logger, Configuration config)
{
	private Dictionary<string, LoadedPlugin> plugins = new();

	public void Init()
	{
		PluginHost.LogMethod = (pluginId, logLevel, message) =>
		{
			LogLevel level = ToILoggerLevel(logLevel);
			logger.Log(level, "[{PluginId}] {Message}", pluginId, message);
		};
		foreach (AvailablePlugin plugin in GetAvailablePlugins())
			if (plugin.State == PluginState.Enabled)
				LoadPlugin(plugin.Path);
	}

	public IEnumerable<AvailablePlugin> GetAvailablePlugins()
	{
		// probably not a good idea
		// should add a method in plugincore that returns only the pluginmeta
		return Directory.GetFiles(config.PluginsPath, "*.zip*")
			.Select(x => (Path: x, new PluginInstance(x).Meta))
			.Select(x => new AvailablePlugin
			{
				Path = Path.GetFileName(x.Path).Replace(".zip.disabled", ".zip"),
				State = x.Path.EndsWith(".disabled") ? PluginState.Disabled :
					plugins.Any(l => l.Value.Metadata.Id == x.Meta.Id) ? PluginState.Loaded : PluginState.Enabled,
				Id = x.Meta.Id,
				Version = x.Meta.Version.Name
			}).ToArray();
	}

	public void EnablePlugin(string path)
	{
		string pluginPath = Path.Join(config.PluginsPath, path);
		string disabledPluginPath = Path.Join(config.PluginsPath, path + ".disabled");
		if (!File.Exists(disabledPluginPath))
			throw new FileNotFoundException($"'{pluginPath}' was not found or is not disabled");
		logger.LogInformation("Enabling plugin {Path}", path);

		File.Move(disabledPluginPath, pluginPath);
		LoadPlugin(path);
	}

	public void DisablePlugin(string path)
	{
		string pluginPath = Path.Join(config.PluginsPath, path);
		try
		{
			UnloadPlugin(plugins.First(x => x.Value.Path == pluginPath).Value.Id);
		}
		catch (InvalidOperationException)
		{
			// ignored
		}

		string disabledPluginPath = Path.Join(config.PluginsPath, path + ".disabled");
		if (!File.Exists(pluginPath))
			throw new FileNotFoundException($"'{pluginPath}' was not found or is already disabled");
		logger.LogInformation("Disabling plugin {Path}", path);

		File.Move(pluginPath, disabledPluginPath);
	}

	public void LoadPlugin(string path)
	{
		logger.LogInformation("Loading plugin {Path}", path);
		string pluginPath = Path.Join(config.PluginsPath, path);
		if (plugins.ContainsKey(pluginPath))
			throw new Exception("Plugin already loaded");

		LoadedPlugin lp = new(pluginPath);
		plugins[lp.Id] = lp;
	}

	public void UnloadPlugin(string id)
	{
		if (!plugins.TryGetValue(id, out LoadedPlugin? plugin))
			throw new KeyNotFoundException("Plugin not loaded");
		logger.LogInformation("Unloading plugin {Path}", id);
		plugin.Dispose();
		plugins.Remove(id);
	}

	public async Task<PluginMeta> InstallPlugin(Stream fileStream)
	{
		string tmp = Path.GetTempFileName();
		try
		{
			FileStream fs = new(tmp, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			await fileStream.CopyToAsync(fs);

			ZipArchive zip = new(fs);
			PluginMeta meta = JsonSerializer.Deserialize<PluginMeta>(zip.Comment)!;
			await zip.DisposeAsync();
			fs.Close();

			string pluginPath = Path.Join(config.PluginsPath, $"{meta.Id}.zip.disabled");
			File.Move(tmp, pluginPath, true);
			return meta;
		}
		catch (JsonException)
		{
			File.Delete(tmp);
			throw new Exception("Invalid plugin file");
		}
		catch
		{
			File.Delete(tmp);
			throw;
		}
	}

	public void DeletePlugin(string path)
	{
		string pluginPath = Path.Join(config.PluginsPath, $"{path}.disabled");
		string enabledPluginPath = Path.Join(config.PluginsPath, path);
		if (!File.Exists(pluginPath))
			throw new Exception(File.Exists(enabledPluginPath)
				? "Please disable the plugin before deleting it."
				: "Plugin not found.");
		File.Delete(pluginPath);
	}

	public void ReloadPlugin(string path)
	{
		string pluginPath = Path.Join(config.PluginsPath, path);
		LoadedPlugin? loaded = plugins.Values.FirstOrDefault(x => x.Path == pluginPath);
		if (loaded != null) UnloadPlugin(loaded.Id);
		LoadPlugin(path);
	}

	private static LogLevel ToILoggerLevel(PluginHost.LogLevel level) => level switch
	{
		PluginHost.LogLevel.Debug => LogLevel.Debug,
		PluginHost.LogLevel.Info => LogLevel.Information,
		PluginHost.LogLevel.Warn => LogLevel.Warning,
		PluginHost.LogLevel.Error => LogLevel.Error,
		_ => LogLevel.Information
	};

	public async Task<T> InvokeForPluginWithId<T>(string id, string method, params object[] args) where T : class
	{
		if (!plugins.TryGetValue(id, out LoadedPlugin? plugin))
			throw new KeyNotFoundException($"Plugin not loaded: {id}");
		return PrependPluginIdToResult(id, await plugin.InvokeFeature<T>(method, args))!;
	}

	public async Task<IEnumerable<T>> InvokeForPluginsWithPermission<T>(string permission, string method,
		params object[] args) where T : class
	{
		Dictionary<string, Task<T>> tasks = plugins.Values.Where(x => x.Metadata.Permissions.Contains(permission))
			.Where(x => x.Metadata.Features.Contains(method))
			.ToDictionary(x =>
				x.Metadata.Permissions.FirstOrDefault(p => p.StartsWith("metadata.provide."))?.Split('.', 3)[2] ??
				x.Metadata.Id, x => x.InvokeFeature<T>(method, args));
		await Task.WhenAll(tasks.Select(async t =>
		{
			try
			{
				await t.Value;
			}
			catch (Exception e)
			{
				logger.LogError(e, "[{Plugin}/{Method}] {Message}", t.Key, method, e.Message);
			}
		}));
		return tasks.Select(x => PrependPluginIdToResult(x.Key, x.Value.Result)).Where(x => x != null).Cast<T>();
	}

	public async Task<IEnumerable<T>> InvokeForPluginsWithFeature<T>(string feature, params object[] args)
		where T : class
	{
		Dictionary<string, Task<T>> tasks = plugins.Values.Where(x => x.Metadata.Features.Contains(feature))
			.ToDictionary(
				x => x.Metadata.Permissions.FirstOrDefault(p => p.StartsWith("metadata.provide."))?.Split('.', 3)[2] ??
				     x.Metadata.Id, x => x.InvokeFeature<T>(feature, args));
		await Task.WhenAll(tasks.Select(async t =>
		{
			try
			{
				await t.Value;
			}
			catch (Exception e)
			{
				logger.LogError(e, "[{Plugin}/{Method}] {Message}", t.Key, feature, e.Message);
			}
		}));
		return tasks.Select(x => PrependPluginIdToResult(x.Key, x.Value.Result)).Where(x => x != null).Cast<T>();
	}

	private static T? PrependPluginIdToResult<T>(string pluginId, T? result) where T : class => result switch
	{
		MetadataSearchResult s => new MetadataSearchResult
		{
			Id = $"{pluginId}:{s.Id}", Titles = s.Titles.Distinct().ToArray(), Poster = s.Poster, Year = s.Year
		} as T,
		MetadataSearchResult[] s => s.Select(x => PrependPluginIdToResult(pluginId, x)).ToArray() as T,
		_ => result
	};
}