namespace OwnStream.ApiModels.Response;

[Obsolete("just replace with PagedResponse")]
public class SearchResponse
{
	public SearchResult[] Results { get; set; }
	public int Total { get; set; }
	public bool HasMore { get; set; }
}