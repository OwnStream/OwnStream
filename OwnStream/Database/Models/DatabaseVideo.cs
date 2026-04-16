namespace OwnStream.Database.Models;

public class DatabaseVideo
{
	public Guid Id { get; set; }
	public Guid LibraryId { get; set; }
	public DatabaseLibrary Library { get; set; } = null!;
	public Guid? EpisodeId { get; set; }
	public DatabaseEpisode? Episode { get; set; } = null!;
	public byte[] EncodingSettings { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public int Fps { get; set; }
	public int Length { get; set; }
	public string Language { get; set; }
}