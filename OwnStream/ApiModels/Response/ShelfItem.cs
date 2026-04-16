namespace OwnStream.ApiModels.Response;

public class ShelfItem
{
	public string Type { get; set; }
	public Guid Id { get; set; }
	public Guid? EpisodeId { get; set; }
	public Guid? VideoId { get; set; }
	public string Title { get; set; }
	public string[] Subtitle { get; set; }
	public string? Image { get; set; }
	public float? WatchProgress { get; set; }
}