using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;

namespace OwnStream.Services;

public class FfmpegJobBackgroundService(
	IServiceScopeFactory scopeFactory,
	ILogger<FfmpegJobBackgroundService> logger,
	JobManager jobManager,
	JobCancellationService jobCancellationService) : BackgroundService
{
	private readonly SemaphoreSlim singleJobLock = new(1, 1);

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await singleJobLock.WaitAsync(cancellationToken);
			try
			{
				DatabaseFfmpegJob? job = await DequeueNextPendingJobAsync(cancellationToken);

				if (job is null)
				{
					await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
					continue;
				}

				await ProcessJobAsync(job.Id, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Unexpected error in FFmpeg background service");
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
			}
			finally
			{
				singleJobLock.Release();
			}
		}
	}

	private async Task<DatabaseFfmpegJob?> DequeueNextPendingJobAsync(CancellationToken cancellationToken)
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();


		DatabaseFfmpegJob? job = await db.FfmpegJobs
			.Where(j => j.Status == DatabaseFfmpegJob.JobStatus.Pending)
			.OrderBy(j => j.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (job == null) return null;

		job.Status = DatabaseFfmpegJob.JobStatus.Starting;
		job.StartedAt = DateTime.UtcNow;

		await db.SaveChangesAsync(cancellationToken);

		return job;
	}

	private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
		DatabaseFfmpegJob? job =
			await db.FfmpegJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

		if (job == null) return;

		using CancellationTokenSource cts =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		jobCancellationService.Register(job.Id, cts);
		try
		{
			logger.LogInformation("Processing job {JobId}", job.Id);

			IJob? ijob = jobManager.GetJobInstance(job.JobType);

			if (ijob == null)
				throw new Exception($"Unexpected job type '{job.JobType}'");

			ijob.Initialize(scope.ServiceProvider);
			await ijob.ExecuteJob(job.Id, cts.Token);

			job.Status = DatabaseFfmpegJob.JobStatus.Completed;
			if (job.ProgressMax != null && job.Progress != job.ProgressMax)
				job.Progress = job.ProgressMax;
			job.CompletedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Job {JobId} failed", job.Id);

			job.Status = DatabaseFfmpegJob.JobStatus.Failed;
			job.Message = string.Join('\n', ex.Message
				.Split('\n')
				// Remove FFmpeg spam
				.Where(x => !x.StartsWith("frame="))
				.Where(x => !x.Contains("Opening") && !x.StartsWith("for reading")));
			job.CompletedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
		}
		finally
		{
			jobCancellationService.Unregister(job.Id, cts);
		}
	}
}
