using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Requests;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/progress/"), Authorize(AuthenticationSchemes = "Cookies,ApiToken")]
public class WatchProgressController(DatabaseContext db) : Controller
{
	[HttpPost("update")]
	public IActionResult UpdateWatchProgress([FromBody] UpdateWatchProgressRequest request)
	{
		bool hasUser = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", out Guid userId);
		DatabaseUser? user = db.Users.Find(userId);

		if (!hasUser || user == null) return Unauthorized();

		bool hasWatchProgress = request is { WatchedMilliseconds: not null, VideoLength: not null };
		bool hasMarkAsWatched = request is { MarkAsWatched: not null };

		if (!hasWatchProgress && !hasMarkAsWatched)
			return BadRequest(
				"Either both WatchedMilliseconds and VideoLength must be provided, or MarkAsWatched must be provided.");

		if (request is { WatchedMilliseconds: not null, VideoLength: null } or
		    { WatchedMilliseconds: null, VideoLength: not null })
			return BadRequest("WatchedMilliseconds and VideoLength must both be provided together.");

		DatabaseWatchProgress? progress = GetWatchProgress(request, user);

		if (progress == null) return NotFound();
		progress.UpdatedAt = DateTimeOffset.UtcNow;
		if (request.WatchedMilliseconds.HasValue)
			progress.MillisecondsWatched = request.WatchedMilliseconds.Value;
		if (request.VideoLength.HasValue)
			progress.VideoLength = request.VideoLength.Value;
		if (request.MarkAsWatched.HasValue)
			progress.FullyWatched = request.MarkAsWatched.Value;
		db.SaveChanges();

		return NoContent();
	}

	private DatabaseWatchProgress? GetWatchProgress(UpdateWatchProgressRequest request, DatabaseUser user)
	{
		DatabaseWatchProgress? existing =
			db.WatchProgress.FirstOrDefault(x => x.VideoId == request.VideoId && x.UserId == user.Id);
		if (existing != null) return existing;

		DatabaseVideo? video = db.Videos
			.Include(x => x.Episode)
			.FirstOrDefault(x => x.Id == request.VideoId);

		if (video == null) return null;
		existing = new DatabaseWatchProgress
		{
			Id = Guid.NewGuid(),
			UserId = user.Id,
			ContentId = video.Episode?.ParentContentId,
			EpisodeId = video.Episode?.Id,
			VideoId = request.VideoId,
			MillisecondsWatched = 0,
			VideoLength = 0,
			FullyWatched = false,
			UpdatedAt = DateTimeOffset.UnixEpoch
		};
		db.WatchProgress.Add(existing);
		return existing;
	}
}