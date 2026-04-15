namespace OwnStream.ApiModels.Response;

public class SubtitleFile
{
	public int Id { get; set; }
	public Dictionary<string, string> Files { get; set; }
	public bool Default { get; set; }
	public bool Forced { get; set; }
	public string Language { get; set; }
	public string Title { get; set; }
}