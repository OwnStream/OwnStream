using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class VideoSegment(DatabaseVideoSegment segment)
{
	public Guid Id { get; set; } = segment.Id;
	public string Type { get; set; } = segment.Type.ToString();
	public int StartMilliseconds { get; set; } = segment.StartMilliseconds;
	public int EndMilliseconds { get; set; } = segment.EndMilliseconds;
	public int VideoDuration { get; set; } = segment.VideoDuration;
}