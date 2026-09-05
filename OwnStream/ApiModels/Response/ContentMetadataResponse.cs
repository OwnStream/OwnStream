using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class ContentMetadataResponse(DatabaseContent content, DatabaseContentExternalId[] externalIds)
{
	public Guid Id { get; set; } = content.Id;
	public DatabaseContent.ContentType Type { get; set; } = content.Type;
	public string Title { get; set; } = content.Title;
	public Dictionary<string, string> TranslatedTitle { get; set; } = content.TranslatedTitle;
	public string Tagline { get; set; } = content.Tagline;
	public Dictionary<string, string> TranslatedTagline { get; set; } = content.TranslatedTagline;
	public string Description { get; set; } = content.Description;
	public Dictionary<string, string> TranslatedDescription { get; set; } = content.TranslatedDescription;

	public string? Poster { get; set; } = content.Poster;
	public string? Banner { get; set; } = content.Banner;
	public string? Logo { get; set; } = content.Logo;
	public string? Backdrop { get; set; } = content.Backdrop;
	public string? Thumbnail { get; set; } = content.Thumbnail;

	public DateTimeOffset CreatedAt { get; set; } = content.CreatedAt;
	public DateTimeOffset UpdatedAt { get; set; } = content.UpdatedAt;
	public DateTimeOffset ReleasedAt { get; set; } = content.ReleasedAt;
	public DateTimeOffset? FinishedStreamingAt { get; set; } = content.FinishedStreamingAt;

	public Dictionary<string, string> AgeRatings { get; set; } = content.AgeRatings;
	public Dictionary<string, string> ExternalIds { get; set; } = externalIds.ToDictionary(x => x.ProviderId, x => x.ExternalId);
}