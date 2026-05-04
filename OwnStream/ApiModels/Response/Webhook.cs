using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Webhook(DatabaseWebhook webhook)
{
	public Guid Id { get; set; } = webhook.Id;
	public Guid LibraryId { get; set; } = webhook.LibraryId;
	public string Name { get; set; } = webhook.Name;
	public bool DeleteOnConvert { get; set; } = webhook.DeleteOnConvert;
}