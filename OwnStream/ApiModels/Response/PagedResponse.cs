namespace OwnStream.ApiModels.Response;

public class PagedResponse<T>
{
	public IEnumerable<T> Items { get; set; }
	public bool HasMore { get; set; }
	public int Count { get; set; }
	public int Pages { get; set; }
}