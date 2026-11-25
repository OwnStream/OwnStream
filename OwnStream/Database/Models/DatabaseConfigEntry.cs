namespace OwnStream.Database.Models;

public class DatabaseConfigEntry
{
	public required string Realm { get; set; }
	public required string Key { get; set; }
	public string? Value { get; set; }
}