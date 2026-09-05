using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class EpisodeMetadataResponse(DatabaseEpisode content)
{
	public Guid Id { get; set; } = content.Id;

	public int Season { get; set; } = content.Season;
	public int Episode { get; set; } = content.Episode;

	public string Title { get; set; } = content.Title;
	public Dictionary<string, string> TranslatedTitle { get; set; } = content.TranslatedTitle;
	public string Summary { get; set; } = content.Summary;
	public Dictionary<string, string> TranslatedSummary { get; set; } = content.TranslatedSummary;
	public string? Thumbnail { get; set; } = content.Thumbnail;

	public DateTimeOffset CreatedAt { get; set; } = content.CreatedAt;
	public DateTimeOffset UpdatedAt { get; set; } = content.UpdatedAt;
	public DateTimeOffset ReleasedAt { get; set; } = content.ReleasedAt;
}