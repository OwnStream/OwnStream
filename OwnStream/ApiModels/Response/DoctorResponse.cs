namespace OwnStream.ApiModels.Response;

public class DoctorResponse
{
	public IEnumerable<Guid> EpisodesWithMultipleVideos { get; set; }
	public IEnumerable<Guid> EpisodesWithNoVideos { get; set; }
	public IEnumerable<Guid> VideosWithNoEpisodes { get; set; }
	public string? FfmpegPath { get; set; }
	public string? FfmpegVersion { get; set; }
}