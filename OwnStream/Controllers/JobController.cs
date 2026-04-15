using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

[Authorize]
public class JobController(DatabaseContext db) : Controller
{
	public IActionResult Index()
	{
		DatabaseFfmpegJob[] jobs = db.FfmpegJobs
			.OrderByDescending(x => x.CompletedAt)
			.ThenByDescending(x => x.CreatedAt)
			.ToArray()
			.OrderByDescending(x =>
			{
				return x.Status switch
				{
					DatabaseFfmpegJob.JobStatus.Pending => 1,
					DatabaseFfmpegJob.JobStatus.Starting => 2,
					DatabaseFfmpegJob.JobStatus.Processing => 2,
					DatabaseFfmpegJob.JobStatus.Completed => 0,
					DatabaseFfmpegJob.JobStatus.Failed => 1,
					_ => throw new ArgumentOutOfRangeException()
				};
			}).ToArray();
		return View(jobs);
	}

	public IActionResult Requeue(Guid id)
	{
		DatabaseFfmpegJob? job = db.FfmpegJobs.Find(id);
		job?.Status = DatabaseFfmpegJob.JobStatus.Pending;
		db.SaveChanges();
		return RedirectToAction("Index");
	}
}