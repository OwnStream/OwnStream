using OwnStream.Plugin;

namespace OwnStream.ApiModels.Response;

public class PluginItem(AvailablePlugin plugin)
{
	public string Id { get; } = plugin.Id;
	public string Version { get; } = plugin.Version;
	public string Path { get; } = plugin.Path;
	public string State { get; } = plugin.State.ToString();
}