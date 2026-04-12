namespace OwnStream.Database.Models;

public class DatabaseWebhook
{
	public Guid Id { get; set; }
	public Guid LibraryId { get; set; }
	public DatabaseLibrary Library { get; set; } = null!;
	public string Name { get; set; }
	public string Authentication { get; set; }
	public bool DeleteOnConvert { get; set; }
}