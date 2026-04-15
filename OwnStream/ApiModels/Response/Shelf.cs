namespace OwnStream.ApiModels.Response;

public class Shelf
{
	public string Title { get; set; }
	public string Type { get; set; }
	public string? Description { get; set; }
	public string? Icon { get; set; }
	public IEnumerable<ShelfItem> Items { get; set; }
}