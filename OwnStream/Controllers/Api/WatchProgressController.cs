using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/progress/"), Authorize(AuthenticationSchemes = "Cookies,ApiToken"), EnableCors("Api")]
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

		// Ignore requests if times are == 0 in case a client sends
		// a request while their player hasn't loaded the video yet
		if (request.WatchedMilliseconds <= 0 || request.VideoLength <= 0)
		{
			progress.UpdatedAt = DateTimeOffset.UtcNow;
			if (request.MarkAsWatched.HasValue) progress.FullyWatched = request.MarkAsWatched.Value;
			return NoContent();
		}

		progress.UpdatedAt = DateTimeOffset.UtcNow;
		if (request.WatchedMilliseconds.HasValue) progress.MillisecondsWatched = request.WatchedMilliseconds.Value;
		if (request.VideoLength.HasValue) progress.VideoLength = request.VideoLength.Value;
		if (request.MarkAsWatched.HasValue) progress.FullyWatched = request.MarkAsWatched.Value;
		db.SaveChanges();

		return NoContent();
	}

	[HttpGet("{id:guid}")]
	public WatchProgressResponse? GetProgress(Guid id)
	{
		bool hasUser = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", out Guid userId);
		DatabaseUser? user = db.Users.Find(userId);

		if (!hasUser || user == null)
		{
			Response.StatusCode = 401;
			return null;
		}

		DatabaseVideo? video = db.Videos.Find(id) ??
		                       db.Episode.Include(x => x.Videos)
			                       .FirstOrDefault(x => x.Id == id)?
			                       .Videos.FirstOrDefault();

		if (video == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseWatchProgress? progress = db.WatchProgress
			.Where(x => x.UserId == user!.Id)
			.Where(x => x.VideoId == video.Id)
			.OrderByDescending(x => x.UpdatedAt)
			.FirstOrDefault();

		return new WatchProgressResponse
		{
			VideoId = video.Id,
			Position = progress?.MillisecondsWatched ?? 0,
			Duration = progress?.VideoLength ?? 0,
			WasMarkedAsWatched = progress?.FullyWatched ?? false
		};
	}

	[HttpGet("upNext/{id:guid}")]
	public EpisodeToWatchResponse? UpNext(Guid id)
	{
		bool hasUser = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", out Guid userId);
		DatabaseUser? user = db.Users.Find(userId);

		if (!hasUser || user == null)
		{
			Response.StatusCode = 401;
			return null;
		}

		DatabaseContent? content = db.Content.Find(id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		if (content.Type == DatabaseContent.ContentType.Movie)
		{
			DatabaseEpisode? episode = db.Episode
				.Include(x => x.Videos)
				.FirstOrDefault(x => x.ParentContentId == id);
			DatabaseWatchProgress movieWatchProgress = db.WatchProgress
				.Include(x => x.Episode)
				.ThenInclude(x => x!.Videos)
				.Where(x => x.UserId == user.Id && x.ContentId == id)
				.OrderByDescending(x => x.UpdatedAt)
				.FirstOrDefault() ?? new DatabaseWatchProgress
			{
				Id = Guid.Empty,
				UserId = Guid.Empty,
				ContentId = content?.Id,
				Content = null,
				EpisodeId = episode?.Id,
				Episode = episode,
				VideoId = Guid.Empty,
				MillisecondsWatched = 0,
				VideoLength = 0,
				FullyWatched = true,
				UpdatedAt = default
			};

			return new EpisodeToWatchResponse
			{
				ContinueWatching = movieWatchProgress.FullyWatched
					? null
					: new Episode(movieWatchProgress.Episode!, HttpContext),
				Progress = movieWatchProgress.FullyWatched ? null : movieWatchProgress.WatchPercentage,
				UpNext = movieWatchProgress.FullyWatched ? new Episode(episode!, HttpContext) : null
			};
		}

		DatabaseWatchProgress? mostRecentWatchProgress = db.WatchProgress
			.Include(x => x.Episode)
			.Where(x => x.UserId == user.Id && x.ContentId == id)
			.OrderByDescending(x => x.UpdatedAt)
			.FirstOrDefault();

		DatabaseWatchProgress? farthestWatchProgress = db.WatchProgress
			.Include(x => x.Episode)
			.ThenInclude(x => x!.Videos)
			.Where(x => x.UserId == user.Id && x.ContentId == id)
			.OrderByDescending(x => x.Episode!.Season)
			.ThenByDescending(x => x.Episode!.Episode)
			.FirstOrDefault();

		if (mostRecentWatchProgress == null)
		{
			DatabaseEpisode? firstEpisode = db.Episode
				.Include(x => x.Videos)
				.Where(x => x.ParentContentId == id)
				.OrderBy(x => x.Season)
				.ThenBy(x => x.Episode)
				.FirstOrDefault();

			return new EpisodeToWatchResponse
			{
				ContinueWatching = null,
				Progress = null,
				UpNext = firstEpisode != null ? new Episode(firstEpisode, HttpContext) : null
			};
		}

		DatabaseEpisode watchingEpisode = mostRecentWatchProgress.Episode!;
		DatabaseEpisode farthestEpisode = farthestWatchProgress?.Episode ?? watchingEpisode;
		DatabaseEpisode? nextEpisode = db.Episode
			                               .Include(x => x.Videos)
			                               .Where(x => x.ParentContentId == id)
			                               .Where(x => x.Season > farthestEpisode.Season ||
			                                           (x.Season == farthestEpisode.Season &&
			                                            x.Episode > farthestEpisode.Episode))
			                               .OrderBy(x => x.Season)
			                               .ThenBy(x => x.Episode)
			                               .FirstOrDefault() ??
		                               // Fallback, show the first episode of the show
		                               db.Episode
			                               .Include(x => x.Videos)
			                               .Where(x => x.ParentContentId == id)
			                               .OrderBy(x => x.Season)
			                               .ThenBy(x => x.Episode)
			                               .FirstOrDefault();

		return new EpisodeToWatchResponse
		{
			ContinueWatching = mostRecentWatchProgress.FullyWatched ? null : new Episode(watchingEpisode, HttpContext),
			Progress = mostRecentWatchProgress.FullyWatched ? null : mostRecentWatchProgress.WatchPercentage,
			UpNext = nextEpisode == null ? null : new Episode(nextEpisode, HttpContext)
		};
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
