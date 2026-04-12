namespace OwnStream.Database.Models;

public class DatabaseFfmpegJob
{
	public Guid Id { get; set; }

	public string JobType { get; set; }
	public string InputPath { get; set; }
	public string OutputPath { get; set; }
	public string Arguments { get; set; }
	public JobStatus Status { get; set; }
	public string? Message { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }

	public enum JobStatus
	{
		Pending = 0,
		Starting = 1,
		Processing = 2,
		Completed = 3,
		Failed = 4
	}
}