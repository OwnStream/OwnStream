namespace OwnStream.ApiModels.Response;

public class EpisodeToWatchResponse
{
	public Episode? ContinueWatching { get; set; }
	public float? Progress { get; set; }
	public Episode? UpNext { get; set; }
}