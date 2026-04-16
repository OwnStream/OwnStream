namespace OwnStream.ApiModels.Response;

public class WatchProgressResponse
{
	public Guid VideoId { get; set; }
	public int Position { get; set; }
	public int Duration { get; set; }
	public bool WasMarkedAsWatched { get; set; }
}