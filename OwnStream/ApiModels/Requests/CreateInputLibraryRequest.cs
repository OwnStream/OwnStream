namespace OwnStream.ApiModels.Requests;

public class CreateInputLibraryRequest
{
	public string Name { get; set; }
	public string Path { get; set; }
	public string Type { get; set; }
	public string TranscodeLibraryId { get; set; }
}