using Humanizer;
using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Content(DatabaseContent content, HttpContext context)
{
	public Guid Id { get; set; } = content.Id;
	public string Type { get; set; } = content.Type.ToString();
	public string OriginalTitle { get; set; } = content.Title;
	public string? TranslatedTitle { get; set; } = content.TranslatedTitle!.GetLocalized(null, context);
	public string OriginalTagline { get; set; } = content.Tagline;
	public string? TranslatedTagline { get; set; } = content.TranslatedTagline!.GetLocalized(null, context);
	public string OriginalDescription { get; set; } = content.Description;
	public string? TranslatedDescription { get; set; } = content.TranslatedDescription!.GetLocalized(null, context);
	public ContentImages Images { get; set; } = new(content);
	public DateTimeOffset CreatedAt { get; set; } = content.CreatedAt;
	public DateTimeOffset UpdatedAt { get; set; } = content.UpdatedAt;
	public DateTimeOffset ReleasedAt { get; set; } = content.ReleasedAt;
	public DateTimeOffset? LastAiredAt { get; set; } = content.FinishedStreamingAt;
	public int? SeasonCount { get; set; } = content.Episodes.DistinctBy(x => x.Season).Count();
	public int? EpisodeCount { get; set; } = content.Episodes.Count;
	public int? VideoCount { get; set; } = content.Episodes.SelectMany(x => x.Videos).Count();

	public string? Runtime { get; set; } = content.Episodes?.SelectMany(x => x.Videos)?.Select(x => x.Length).Sum()
		.Milliseconds().ToString(@"hh\:mm\:ss");

	public Dictionary<string, string> AgeRatings { get; set; } = content.AgeRatings;

	public Dictionary<string, string> ExternalIds { get; set; } = new Dictionary<string, string?>()
		{
			["imdb"] = content.ImdbId,
			["tmdb"] = content.TmdbId?.ToString(),
			["tvdb"] = content.TvdbId?.ToString(),
			["tvMaze"] = content.TvMazeId?.ToString()
		}
		.Where(x => x.Value != null)
		.ToDictionary(x => x.Key, x => x.Value!);

	public class ContentImages(DatabaseContent content)
	{
		public string? Poster { get; set; } = content.Poster;
		public string? Banner { get; set; } = content.Banner;
		public string? Logo { get; set; } = content.Logo;
		public string? Backdrop { get; set; } = content.Backdrop;
		public string? Thumbnail { get; set; } = content.Thumbnail;
	}
}