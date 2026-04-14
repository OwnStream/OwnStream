namespace OwnStream.Database.Models;

public class DatabaseContent
{
	public Guid Id { get; set; }
	public Guid LibraryId { get; set; }
	public DatabaseLibrary Library { get; set; } = null!;
	public ContentType Type { get; set; }
	public string Title { get; set; }
	public Dictionary<string, string> TranslatedTitle { get; set; } = [];
	public string Tagline { get; set; }
	public Dictionary<string, string> TranslatedTagline { get; set; } = [];
	public string Description { get; set; }
	public Dictionary<string, string> TranslatedDescription { get; set; } = [];

	public string? Poster { get; set; } = null;
	public string? Banner { get; set; } = null;
	public string? Logo { get; set; } = null;
	public string? Backdrop { get; set; } = null;
	public string? Thumbnail { get; set; } = null;
	
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public DateTimeOffset ReleasedAt { get; set; }
	public DateTimeOffset? FinishedStreamingAt { get; set; }
	
	public Dictionary<string, string> AgeRatings { get; set; } = [];
	public string? ImdbId { get; set; }
	public int? TmdbId { get; set; }
	public ICollection<DatabaseEpisode> Episodes { get; } = new List<DatabaseEpisode>();

	public enum ContentType
	{
		Movie,
		Tv
	}
}