using Xabe.FFmpeg;

namespace OwnStream.Services.JobArguments;

public class TranscodeJobArguments
{
	public ResolutionInfo[] Resolutions { get; set; }
	public AudioResolutionInfo[] AudioResolutions { get; set; }
	public bool DeleteAfterTranscode { get; set; }
	public Dictionary<string, string> Metadata { get; set; }
	public Guid VideoId { get; set; }

	public class ResolutionInfo
	{
		public string Name { get; set; }
		public int Width { get; set; }
		public int Bitrate { get; set; }
		public string Codec { get; set; }
	}

	public class AudioResolutionInfo
	{
		public int Bitrate { get; set; }
		public int Channels { get; set; }
		public string Codec { get; set; }
	}
}