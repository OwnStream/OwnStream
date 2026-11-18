using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Season(int index, int count, DatabaseSeason? season, HttpContext context)
{
	public int Index { get; set; } = index;
	public int EpisodeCount { get; set; } = count;

	public string? Title { get; set; } = season?.Title;
	public string? Description { get; set; } = season?.Description;
	public string? TranslatedTitle { get; set; } = season?.TranslatedTitle.GetLocalized(null, context);
	public string? TranslatedDescription { get; set; } = season?.TranslatedDescription.GetLocalized(null, context);

	public string? Poster { get; set; } = season?.Poster;
}