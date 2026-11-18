namespace OwnStream.ApiModels.Requests;

public class SeasonMetadataEditRequest()
{
	public string? Title { get; set; }
	public Dictionary<string, string?>? TranslatedTitle { get; set; }
	public string? Description { get; set; }
	public Dictionary<string, string?>? TranslatedDescription { get; set; }

	public string? Poster { get; set; }
}