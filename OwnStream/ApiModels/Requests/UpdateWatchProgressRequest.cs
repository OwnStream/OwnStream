namespace OwnStream.ApiModels.Requests;

public class UpdateWatchProgressRequest
{
	public Guid VideoId { get; set; }
	public int? VideoLength { get; set; }
	public int? WatchedMilliseconds { get; set; }
	public bool? MarkAsWatched { get; set; }
}