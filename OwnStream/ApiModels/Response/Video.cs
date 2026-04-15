using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Video(DatabaseVideo video)
{
	public Guid Id { get; set; } = video.Id;
	public byte[] EncodingSettings { get; set; } = video.EncodingSettings;
	public int Width { get; set; } = video.Width;
	public int Height { get; set; } = video.Height;
	public int Fps { get; set; } = video.Fps;
	public string Language { get; set; } = video.Language;

	public SubtitleFile[]? Subtitles { get; set; } = null;
	public PreviewFile[]? PreviewFiles { get; set; } = null;
	public Episode? Episode { get; set; } = null;
}