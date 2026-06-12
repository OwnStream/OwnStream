namespace OwnStream.Database.Models;

public class DatabaseInputLibrary
{
	public Guid Id { get; set; }
	public Guid TranscodeLibraryId { get; set; }
	public DatabaseLibrary TranscodeLibrary { get; set; } = null!;
	public string Name { get; set; }
	public string Path { get; set; }
	public InputLibraryType Type { get; set; }

	public enum InputLibraryType
	{
		Movie,
		Tv
	}
}