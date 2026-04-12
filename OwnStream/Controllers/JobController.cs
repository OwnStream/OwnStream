using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

[Authorize]
public class JobController(DatabaseContext db) : Controller
{
	public IActionResult Index() => View(db.FfmpegJobs.ToArray());

	public IActionResult Requeue(Guid id)
	{
		DatabaseFfmpegJob? job = db.FfmpegJobs.Find(id);
		job?.Status = DatabaseFfmpegJob.JobStatus.Pending;
		db.SaveChanges();
		return RedirectToAction("Index");
	}
}