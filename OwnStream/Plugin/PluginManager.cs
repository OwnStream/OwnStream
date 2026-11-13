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
		try
		{
			UnloadPlugin(plugins.First(x => x.Value.Path == path).Value.Id);
		}
		catch (KeyNotFoundException)
		{
			// ignored
		}

		string pluginPath = Path.Join(config.PluginsPath, path);
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

	public string[] ListLoadedPlugins()
	{
		return plugins.Values.Select(x => x.Metadata.Id).ToArray();
	}

	private static LogLevel ToILoggerLevel(PluginHost.LogLevel level) => level switch
	{
		PluginHost.LogLevel.Debug => LogLevel.Debug,
		PluginHost.LogLevel.Info => LogLevel.Information,
		PluginHost.LogLevel.Warn => LogLevel.Warning,
		PluginHost.LogLevel.Error => LogLevel.Error,
		_ => LogLevel.Information
	};

	public async Task<IEnumerable<T>> InvokeForPluginsWithPermission<T>(string permission, string method,
		params object[] args) where T : class
	{
		Dictionary<string, Task<T>> tasks = plugins.Values.Where(x => x.Metadata.Permissions.Contains(permission))
			.ToDictionary(x =>
				x.Metadata.Permissions.FirstOrDefault(p => p.StartsWith("metadata.provide."))?.Split('.', 3)[2] ??
				x.Metadata.Id, x => x.InvokeFeature<T>(method, args));
		await Task.WhenAll(tasks.Values);
		return tasks.Select(x => PrependPluginIdToResult(x.Key, x.Value.Result)!);
	}

	public async Task<IEnumerable<T>> InvokeForPluginsWithFeature<T>(string feature, params object[] args)
		where T : class
	{
		Dictionary<string, Task<T>> tasks = plugins.Values.Where(x => x.Metadata.Features.Contains(feature))
			.ToDictionary(
				x => x.Metadata.Permissions.FirstOrDefault(p => p.StartsWith("metadata.provide."))?.Split('.', 3)[2] ??
				     x.Metadata.Id, x => x.InvokeFeature<T>(feature, args));
		await Task.WhenAll(tasks.Values);
		return tasks.Select(x => PrependPluginIdToResult(x.Key, x.Value.Result)!);
	}

	private static T? PrependPluginIdToResult<T>(string pluginId, T result) where T : class => result switch
	{
		MetadataSearchResult s => new MetadataSearchResult
		{
			Id = $"{pluginId}:{s.Id}", Titles = s.Titles, Poster = s.Poster, Year = s.Year
		} as T,
		MetadataSearchResult[] s => s.Select(x => PrependPluginIdToResult(pluginId, x)).ToArray() as T,
		_ => result
	};
}