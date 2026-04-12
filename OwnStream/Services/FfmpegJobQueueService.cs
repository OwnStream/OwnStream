using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Services;

public interface IFfmpegJobQueueService
{
	Task<DatabaseFfmpegJob> EnqueueAsync(string jobType, string inputPath, string outputPath, object arguments);
	Task<List<DatabaseFfmpegJob>> GetPendingJobsAsync();
}

public class FfmpegJobQueueService(IServiceScopeFactory scopeFactory) : IFfmpegJobQueueService
{
	public async Task<DatabaseFfmpegJob> EnqueueAsync(string jobType, string inputPath, string outputPath,
		object arguments)
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

		DatabaseFfmpegJob job = new()
		{
			Id = Guid.NewGuid(),
			JobType = jobType,
			InputPath = inputPath,
			OutputPath = outputPath,
			Arguments = JsonSerializer.Serialize(arguments),
			Status = DatabaseFfmpegJob.JobStatus.Pending,
			CreatedAt = DateTime.UtcNow
		};

		db.FfmpegJobs.Add(job);
		await db.SaveChangesAsync();
		return job;
	}

	public async Task<List<DatabaseFfmpegJob>> GetPendingJobsAsync()
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

		return await db.FfmpegJobs
			.Where(j => j.Status == DatabaseFfmpegJob.JobStatus.Pending)
			.OrderBy(j => j.CreatedAt)
			.ToListAsync();
	}
}