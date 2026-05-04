namespace OwnStream.ApiModels.Requests;

public class CreateWebhookRequest
{
	public string Name { get; set; }
	public string Authentication { get; set; }
	public bool DeleteOnConvert { get; set; }
	public string LibraryId { get; set; }
}