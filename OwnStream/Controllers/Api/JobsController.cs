using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/jobs/")]
public class JobsController(DatabaseContext db) : Controller
{
	[Route("list"), Authorize(Roles = nameof(UserPermissions.ReadJobs), AuthenticationSchemes = "ApiToken")]
	public PagedResponse<Job> GetJobs(long delta = 0, int page = 0, int limit = 20)
	{
		DateTimeOffset lastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(delta);
		IQueryable<DatabaseFfmpegJob> query = db.FfmpegJobs.Where(x => x.UpdatedAt > lastUpdated);
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
}