using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Requests;

public class ContentMetadataEditRequest()
{
	public DatabaseContent.ContentType? Type { get; set; }
	public string? Title { get; set; }
	public Dictionary<string, string?>? TranslatedTitle { get; set; }
	public string? Tagline { get; set; }
	public Dictionary<string, string?>? TranslatedTagline { get; set; }
	public string? Description { get; set; }
	public Dictionary<string, string?>? TranslatedDescription { get; set; }

	public string? Poster { get; set; }
	public string? Banner { get; set; }
	public string? Logo { get; set; }
	public string? Backdrop { get; set; }
	public string? Thumbnail { get; set; }

	public DateTimeOffset? ReleasedAt { get; set; }
	public DateTimeOffset? FinishedStreamingAt { get; set; }

	public Dictionary<string, string?>? AgeRatings { get; set; }
	public Dictionary<string, string?>? ExternalIds { get; set; }
}