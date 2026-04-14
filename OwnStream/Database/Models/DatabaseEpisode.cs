namespace OwnStream.Database.Models;

public class DatabaseEpisode
{
	public Guid Id { get; set; }
	public Guid ParentContentId { get; set; }
	public DatabaseContent ParentContent { get; set; } = null!;
	
	public int Season { get; set; }
	public int Episode { get; set; }

	public string Title { get; set; }
	public Dictionary<string, string> TranslatedTitle { get; set; } = [];
	public string Summary { get; set; }
	public Dictionary<string, string> TranslatedSummary { get; set; } = [];
	public string? Thumbnail { get; set; }
	
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public DateTimeOffset ReleasedAt { get; set; }
	public ICollection<DatabaseVideo> Videos { get; } = new List<DatabaseVideo>();
}