using OwnStream.Database.Models;

namespace OwnStream.ApiModels.Response;

public class Job(DatabaseFfmpegJob job)
{
	public Guid Id { get; set; }= job.Id;
	public string JobType { get; set; }= job.JobType;
	public string Status { get; set; }= job.Status.ToString();
	public string? Message { get; set; }= job.Message;
	public int? Progress { get; set; }= job.Progress;
	public int? ProgressMax { get; set; }= job.ProgressMax;
	public DateTimeOffset CreatedAt { get; set; }= job.CreatedAt;
	public DateTimeOffset? StartedAt { get; set; }= job.StartedAt;
	public DateTimeOffset? UpdatedAt { get; set; }= job.UpdatedAt;
	public DateTimeOffset? CompletedAt { get; set; }= job.CompletedAt;
}