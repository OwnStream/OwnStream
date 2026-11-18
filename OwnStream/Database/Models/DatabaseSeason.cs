namespace OwnStream.Database.Models;

public class DatabaseSeason
{
	public Guid ContentId { get; set; }
	public int Index { get; set; }

	public string Title { get; set; } = "";
	public Dictionary<string, string> TranslatedTitle { get; set; } = [];
	public string Description { get; set; } = "";
	public Dictionary<string, string> TranslatedDescription { get; set; } = [];

	public string? Poster { get; set; } = null;

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }

	public DatabaseContent Content { get; set; } = null!;
}
