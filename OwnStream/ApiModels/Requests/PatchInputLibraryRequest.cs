namespace OwnStream.ApiModels.Requests;

public class PatchInputLibraryRequest
{
	public string? Name { get; set; }
	public string? Type { get; set; }
	public string? TranscodeLibraryId { get; set; }
}