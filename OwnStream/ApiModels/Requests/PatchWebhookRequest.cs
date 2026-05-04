namespace OwnStream.ApiModels.Requests;

public class PatchWebhookRequest
{
	public string? Name { get; set; }
	public string? Authentication { get; set; }
	public bool? DeleteOnConvert { get; set; }
	public string? LibraryId { get; set; }
}