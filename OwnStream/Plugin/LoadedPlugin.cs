using OwnStream.PluginCore;

namespace OwnStream.Plugin;

public class LoadedPlugin(string path) : IDisposable
{
	public string Path => path;
	private readonly PluginInstance instance = new(path);
	public PluginMeta Metadata => instance.Meta;
	public string Id => Metadata.Id;

	public async Task<T> InvokeFeature<T>(string method, params object[] args) =>
		(await instance.InvokeFeature<T>(method, args))!;

	public void Dispose()
	{
		instance.Dispose();
	}
}