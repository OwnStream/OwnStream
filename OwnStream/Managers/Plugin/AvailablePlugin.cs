namespace OwnStream.Managers.Plugin;

public class AvailablePlugin
{
	public string Path { get; set; }
	public PluginState State { get; set; }
	public string Id { get; set; }
	public string Version { get; set; }
}

public enum PluginState
{
	Disabled,
	Enabled,
	Loaded
}