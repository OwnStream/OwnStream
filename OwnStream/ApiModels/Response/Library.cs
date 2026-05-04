using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Library(DatabaseLibrary library, bool includeDiskUsage = false)
{
	public Guid Id { get; set; } = library.Id;
	public string Name { get; set; } = library.Name;
	public string Path { get; set; } = library.Path;
	public DiskInfo? DiskUsage { get; set; } = includeDiskUsage ? library.GetSpaceInfo() : null;
}