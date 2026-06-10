namespace OwnStream.ApiModels.Response;

public class SearchResult
{
	public string Kind { get; set; }
	public Guid Id { get; set; }

	public Guid? ContentId { get; set; }
	public string? ContentTitle { get; set; }

	public string Title { get; set; }
	public string? Description { get; set; }

	public string? TranslatedTitle { get; set; }
	public string? TranslatedDescription { get; set; }
	public Content.ContentImages Images { get; set; }

	public int? Season { get; set; }
	public int? Episode { get; set; }
}