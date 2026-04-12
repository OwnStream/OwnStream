namespace OwnStream.Database.Models;

public class DatabaseVideo
{
	public Guid Id { get; set; }
	public byte[] EncodingSettings { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public int Fps { get; set; }
	public string Language { get; set; }
}