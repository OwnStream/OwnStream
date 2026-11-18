using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class SeasonMetadataResponse(DatabaseSeason season)
{
	public Guid ContentId { get; set; } = season.ContentId;
	public int Index { get; set; } = season.Index;
	public string Title { get; set; } = season.Title;
	public Dictionary<string, string> TranslatedTitle { get; set; } = season.TranslatedTitle;
	public string Description { get; set; } = season.Description;
	public Dictionary<string, string> TranslatedDescription { get; set; } = season.TranslatedDescription;

	public string? Poster { get; set; } = season.Poster;
}