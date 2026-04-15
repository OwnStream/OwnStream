namespace OwnStream.ApiModels.Response;

public class PreviewFile
{
	public string Template { get; set; }
	public int FrameCount { get; set; }
	public int Rows { get; set; }
	public int Columns { get; set; }
	public int? Period { get; set; }
}