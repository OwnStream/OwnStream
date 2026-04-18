using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OwnStream;

public class TranscodeConfiguration
{
	public int MaxVideoStreams { get; set; } = 2;
	public List<VideoPreset> VideoPresets { get; set; } = [];

	public List<AudioPreset> AudioPresets { get; set; } = [];
	public List<string> AudioLanguages { get; set; } = [];
	public PixFmtHandling PixelFormatHandling { get; set; } = PixFmtHandling.DownsampleAlways;

	public class AudioPreset
	{
		public int Bitrate { get; set; }
		public int Channels { get; set; }
		public string Codec { get; set; }
	}

	public class VideoPreset
	{
		public string Name { get; set; }
		public int Width { get; set; }
		public int Bitrate { get; set; }
		public string Codec { get; set; }
	}

	public enum PixFmtHandling
	{
		DownsampleIfUnsupported = 0,
		DownsampleAlways = 1,
		UseSoftware = 2,
		Skip = 3
	}

	public bool IsValid() => VideoPresets.Count > 0 && AudioPresets.Count > 0 && MaxVideoStreams > 0;
	public byte[] GetHash() => MD5.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));
}