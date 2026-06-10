namespace OwnStream.Database.Models;

public class DatabaseContentExternalId
{
	public Guid ContentId { get; set; }
	public DatabaseContent Content { get; set; } = null!;

	public string ProviderId { get; set; } = null!;
	public string ExternalId { get; set; } = null!;
}