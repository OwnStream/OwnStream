using Humanizer;
using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Episode(DatabaseEpisode episode, HttpContext? context = null)
{
	public Guid Id { get; set; } = episode.Id;

	public int SeasonNumber { get; set; } = episode.Season;
	public int EpisodeNumber { get; set; } = episode.Episode;

	public string OriginalTitle { get; set; } = episode.Title;
	public string? TranslatedTitle { get; set; } = episode.TranslatedTitle!.GetLocalized(null, context);
	public string OriginalSummary { get; set; } = episode.Summary;
	public string? TranslatedSummary { get; set; } = episode.TranslatedSummary!.GetLocalized(null, context);
	public string? Thumbnail { get; set; } = episode.Thumbnail;

	public string? Runtime { get; set; } =
		episode.Videos?.FirstOrDefault()?.Length.Milliseconds().ToString(@"hh\:mm\:ss");

	public DateTimeOffset CreatedAt { get; set; } = episode.CreatedAt;
	public DateTimeOffset UpdatedAt { get; set; } = episode.UpdatedAt;
	public DateTimeOffset ReleasedAt { get; set; } = episode.ReleasedAt;
	public IEnumerable<Video> Videos { get; set; } = episode.Videos.Select(x => new Video(x));
}