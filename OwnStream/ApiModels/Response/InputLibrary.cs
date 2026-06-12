using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class InputLibrary(DatabaseInputLibrary library)
{
	public Guid Id { get; set; } = library.Id;
	public string Name { get; set; } = library.Name;
	public string Path { get; set; } = library.Path;
	public string Type { get; set; } = library.Type.ToString();
	public Guid TranscodeLibraryId { get; set; } = library.TranscodeLibraryId;
}