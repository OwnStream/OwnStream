namespace OwnStream.Database.Models;

public class DatabaseContentTag
{
	public Guid Id { get; set; }
	public Guid ContentId { get; set; }
	public DatabaseContent? Content { get; set; }
	public bool IsSystem { get; set; }
	public string Tag { get; set; }
}