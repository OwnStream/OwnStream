namespace OwnStream.ApiModels.Response;

public class SearchResponse
{
	public SearchResult[] Results { get; set; }
	public int Total { get; set; }
	public bool HasMore { get; set; }
}