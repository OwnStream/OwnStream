using System.Text.Json;
using System.Text.Json.Serialization;

namespace OwnStream;

public class Configuration
{
	[JsonIgnore]
	public string ConfigPath => Path.Join(Utils.GetEnvironmentVariable("OWNSTREAM_DATA_PATH")!, "config.json");
	
	public TranscodeConfiguration Transcode { get; set; } = new();

	public static Configuration LoadConfiguration()
	{
		string path = Path.Join(Utils.GetEnvironmentVariable("OWNSTREAM_DATA_PATH")!, "config.json");
		Configuration? config = null;
		if (File.Exists(path))
		{
			using FileStream fs = File.OpenRead(path);
			config = JsonSerializer.Deserialize<Configuration>(fs);
		}

		config ??= new Configuration();
		return config;
	}

	public void SaveConfiguration()
	{
		if (!Directory.Exists(Path.GetDirectoryName(ConfigPath)))
			Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
		File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this));
	}
}