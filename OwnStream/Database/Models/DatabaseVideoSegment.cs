namespace OwnStream.Database.Models;

public class DatabaseVideoSegment
{
	public Guid Id { get; set; }
	public Guid VideoId { get; set; }
	public DatabaseVideo Video { get; set; }
	public SegmentType Type { get; set; }
	public int StartMilliseconds { get; set; }
	public int EndMilliseconds { get; set; }
	public int VideoDuration { get; set; }
}

public enum SegmentType
{
	Opening,
	Ending,
	Intermission
}