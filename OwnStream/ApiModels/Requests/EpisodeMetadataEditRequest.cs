namespace OwnStream.ApiModels.Requests;

public class EpisodeMetadataEditRequest()
{
	public int? Season { get; set; }
	public int? Episode { get; set; }

	public string? Title { get; set; }
	public Dictionary<string, string>? TranslatedTitle { get; set; }
	public string? Summary { get; set; }
	public Dictionary<string, string>? TranslatedSummary { get; set; }
	public string? Thumbnail { get; set; }

	public DateTimeOffset? ReleasedAt { get; set; }
}