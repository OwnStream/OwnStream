using System.ComponentModel.DataAnnotations.Schema;

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
	public int? Progress { get; set; }
	public int? ProgressMax { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? UpdatedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }

	public Guid? RelevantVideoId { get; set; }
	public DatabaseVideo? RelevantVideo { get; set; } = null;
	public Guid? RelevantEpisodeId { get; set; }
	public DatabaseEpisode? RelevantEpisode { get; set; } = null;
	public Guid? RelevantContentId { get; set; }
	public DatabaseContent? RelevantContent { get; set; } = null;
	public Guid? RelevantLibraryId { get; set; }
	public DatabaseLibrary? RelevantLibrary { get; set; } = null;
	public Guid? RelevantWebhookId { get; set; }
	public DatabaseWebhook? RelevantWebhook { get; set; } = null;

	[NotMapped]
	public float? GetPercentage =>
		Progress != null && ProgressMax != null ? ((float)Progress / (float)ProgressMax) * 100 : null;

	public enum JobStatus
	{
		Pending = 0,
		Starting = 1,
		Processing = 2,
		Completed = 3,
		Failed = 4
	}
}