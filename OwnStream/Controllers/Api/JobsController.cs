using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Services;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/jobs/"), EnableCors("Api")]
public class JobsController(DatabaseContext db, JobCancellationService jobCancellationService) : Controller
{
	[HttpGet("list"), Authorize(Roles = nameof(UserPermissions.ReadJobs))]
	public PagedResponse<Job> GetJobs(long delta = 0, int page = 0, int limit = 20)
	{
		DateTimeOffset lastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(delta);
		IQueryable<DatabaseFfmpegJob> query = db.FfmpegJobs
			.OrderByDescending(x => x.UpdatedAt)
			.Where(x => x.UpdatedAt > lastUpdated);
		int count = query.Count();
		int pages = (int)Math.Ceiling(count / (float)limit);
		return new PagedResponse<Job>
		{
			Items = query
				.Skip(page * limit)
				.Take(limit)
				.ToArray()
				.Select(x => new Job(x)),
			HasMore = pages > page + 1,
			Count = count,
			Pages = pages
		};
	}

	[HttpGet("{id:guid}/requeue"),
	 Authorize(Roles = nameof(UserPermissions.WriteJobs))]
	public SuccessResponse<Job> RequeueJob(Guid id)
	{
		DatabaseFfmpegJob? job = db.FfmpegJobs.Find(id);

		if (job == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<Job>(false, "Job not found");
		}

		if (job.Status is not (DatabaseFfmpegJob.JobStatus.Completed or DatabaseFfmpegJob.JobStatus.Failed))
		{
			Response.StatusCode = 400;
			return new SuccessResponse<Job>(false, "Job is not completed or failed");
		}

		job.Status = DatabaseFfmpegJob.JobStatus.Pending;
		job.Progress = null;
		job.ProgressMax = null;
		job.Message = $"Requeued by user {HttpContext.User.FindFirstValue(ClaimTypes.Name)}";
		db.SaveChanges();

		return new SuccessResponse<Job>(true, "", new Job(job));
	}

	[HttpGet("{id:guid}/stop"),
	 Authorize(Roles = nameof(UserPermissions.WriteJobs))]
	public SuccessResponse<Job> StopJob(Guid id)
	{
		DatabaseFfmpegJob? job = db.FfmpegJobs.Find(id);

		if (job == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<Job>(false, "Job not found");
		}

		if (job.Status is not (DatabaseFfmpegJob.JobStatus.Pending or DatabaseFfmpegJob.JobStatus.Processing))
		{
			Response.StatusCode = 400;
			return new SuccessResponse<Job>(false, "Job is not running/waiting to be ran");
		}

		if (job.Status == DatabaseFfmpegJob.JobStatus.Processing)
		{
			if (!jobCancellationService.TryCancel(job.Id))
			{
				return new SuccessResponse<Job>(false, "Failed to request job cancellation.", new Job(job));
			}
		}

		job.Status = DatabaseFfmpegJob.JobStatus.Failed;
		job.Progress = null;
		job.ProgressMax = null;
		job.Message = $"Cancelled by user {HttpContext.User.FindFirstValue(ClaimTypes.Name)}\n{job.Message}";
		db.SaveChanges();

		return new SuccessResponse<Job>(true, "", new Job(job));
	}
}