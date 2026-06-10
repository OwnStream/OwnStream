using System.Security.Claims;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/home"), Authorize, EnableCors("Api")]
public class HomeController(DatabaseContext db) : Controller
{
	[HttpGet("shelves")]
	public IEnumerable<Shelf> GetShelves() => new List<Shelf>()
	{
		new() { Title = "Reminder to migrate away from /api/home/shelves", Type = "message", Items = [] },
		new() { Title = "Next Up", Type = "nextup", Items = NextUp() },
		new() { Title = "Continue Watching", Type = "continue", Items = ContinueWatching() },
		new() { Title = "Recently Added", Type = "recent", Items = RecentlyAdded() },
		new() { Title = "Movies", Type = "movie", Items = RecentlyAddedMovies() },
		new() { Title = "Shows", Type = "tv", Items = RecentlyAddedTv() }
	}.Where(x => x.Items.Any() || x.Type == "message");

	[HttpGet("nextUp")]
	public IEnumerable<ShelfItem> NextUp()
	{
		Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
		return db.WatchProgress
			.Include(x => x.Episode)
			.Include(x => x.Content)
			.ThenInclude(x => x!.Episodes)
			.Where(x => x.UserId == userId)
			.Where(x => x.Content != null)
			.OrderByDescending(x => x.UpdatedAt)
			.Where(x => x.Content!.Type == DatabaseContent.ContentType.Tv)
			.GroupBy(x => x.ContentId)
			.ToArray()
			.Select(group => group
				.OrderByDescending(x => x.Episode!.Season)
				.ThenByDescending(x => x.Episode!.Episode)
				.First())
			.Where(x => x.FullyWatched)
			.Select(x =>
			{
				DatabaseEpisode? nextEpisode = db.Episode
					.Include(e => e.ParentContent)
					.Include(e => e.Videos)
					.Where(e => e.ParentContentId == x.ContentId)
					.Where(e => e.Season > x.Episode!.Season ||
					            (e.Season == x.Episode.Season && e.Episode > x.Episode.Episode))
					.Where(e => e.Videos.Any())
					.OrderBy(e => e.Season)
					.ThenBy(e => e.Episode)
					.FirstOrDefault();

				return nextEpisode;
			})
			.Where(x => x != null)
			.Cast<DatabaseEpisode>()
			.Take(10)
			.Select(x => new ShelfItem
			{
				Type = "episode",
				Id = x.ParentContentId,
				EpisodeId = x.Id,
				VideoId = x.Videos.FirstOrDefault()?.Id,
				Title = x.ParentContent.TranslatedTitle.GetLocalized(x.ParentContent.Title, HttpContext)!,
				Subtitle =
				[
					"S: " + x.Season + "  E: " + x.Episode,
					x.TranslatedTitle.GetLocalized(x.Title, HttpContext)!,
					(x.Videos.FirstOrDefault()?.Length ?? 0).Milliseconds().ToString("h", "m")
				],
				Image = x.Thumbnail,
				WatchProgress = null
			});
	}

	[HttpGet("continue")]
	public IEnumerable<ShelfItem> ContinueWatching()
	{
		Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
		return db.WatchProgress
			.Include(x => x.Episode)
			.Include(x => x.Content)
			.ThenInclude(x => x!.Episodes)
			.ThenInclude(x => x!.Videos)
			.Where(x => x.UserId == userId)
			.Where(x => x.Content != null)
			.OrderByDescending(x => x.UpdatedAt)
			.GroupBy(x => x.ContentId)
			.ToArray()
			.Select(x => x.MaxBy(y => y.UpdatedAt))
			.Cast<DatabaseWatchProgress>()
			.Where(x => !x.FullyWatched)
			.Take(10)
			.Select(x => new ShelfItem
			{
				Type = x.Content?.Type switch
				{
					DatabaseContent.ContentType.Movie => "movie",
					DatabaseContent.ContentType.Tv => "episode",
					_ => "video"
				},
				Id = x.ContentId ?? x.VideoId,
				EpisodeId = x.EpisodeId,
				VideoId = x.VideoId,
				Title = x.Content?.TranslatedTitle.GetLocalized(x.Content?.Title, HttpContext)!,
				Subtitle = x.Content?.Type switch
				{
					DatabaseContent.ContentType.Movie =>
					[
						Math.Round((x.Episode?.Videos.FirstOrDefault()?.Length ?? 0) *
						           ((100 - x.WatchPercentage) / 100)).Milliseconds().ToString("h", "m") +
						" left"
					],
					DatabaseContent.ContentType.Tv =>
					[
						"S: " + x.Episode?.Season + "  E: " + x.Episode?.Episode,
						x.Episode?.TranslatedTitle.GetLocalized(x.Episode?.Title, HttpContext)!,
						Math.Round((x.Episode?.Videos.FirstOrDefault()?.Length ?? 0) *
						           ((100 - x.WatchPercentage) / 100)).Milliseconds().ToString("h", "m") +
						" left"
					],
					_ => []
				},
				Image = x.Content?.Type switch
				{
					DatabaseContent.ContentType.Movie => x.Content.Poster,
					_ => x.Episode?.Thumbnail
				},
				WatchProgress = x.WatchPercentage
			});
	}

	[HttpGet("recent")]
	public IEnumerable<ShelfItem> RecentlyAdded(int? cutoffDays = null)
	{
		Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
		DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Abs(cutoffDays ?? 30));
		return db.Episode
			.Include(x => x.ParentContent)
			.Include(x => x.Videos)
			.Include(x => x.WatchProgresses)
			.Where(x => x.UpdatedAt > cutoff)
			.Where(x => !x.WatchProgresses.Any(progress => progress.UserId == userId && progress.FullyWatched))
			.OrderByDescending(x => x.UpdatedAt)
			.Take(50)
			.ToArray()
			.GroupBy(x => x.ParentContentId)
			.Select(x => x.FirstOrDefault())
			.Where(x => x != null)
			.Cast<DatabaseEpisode>()
			.Take(10)
			.Select(x => new ShelfItem
			{
				Type = x.ParentContent.Type switch
				{
					DatabaseContent.ContentType.Movie => "movie",
					DatabaseContent.ContentType.Tv => "episode",
					_ => "video"
				},
				Id = x.ParentContentId,
				EpisodeId = x.Id,
				VideoId = x.Videos.FirstOrDefault()?.Id,
				Title = x.ParentContent.TranslatedTitle.GetLocalized(x.ParentContent.Title, HttpContext)!,
				Subtitle =
					x.ParentContent.Type switch
					{
						DatabaseContent.ContentType.Movie =>
						[
							x.ParentContent.ReleasedAt.Year.ToString(),
							(x.Videos.FirstOrDefault()?.Length ?? 0).Milliseconds().ToString("h", "m")
						],
						DatabaseContent.ContentType.Tv =>
						[
							$"S: {x.Season} E: {x.Episode}",
							x.TranslatedTitle.GetLocalized(x.Title, HttpContext)!,
							(x.Videos.FirstOrDefault()?.Length ?? 0).Milliseconds().ToString("h", "m")
						],
						_ => []
					},
				Image = x.ParentContent.Type switch
				{
					DatabaseContent.ContentType.Movie => x.ParentContent.Poster,
					_ => x.Thumbnail
				},
				WatchProgress = x.WatchProgresses.FirstOrDefault(y => !y.FullyWatched && y.UserId == userId)
					?.WatchPercentage
			});
	}

	[HttpGet("recentContent/movie")]
	public IEnumerable<ShelfItem> RecentlyAddedMovies(int? cutoffDays = null) => db.Content
		.Include(x => x.Episodes)
		.ThenInclude(x => x.Videos)
		.Where(x => x.Type == DatabaseContent.ContentType.Movie)
		.OrderByDescending(x => x.UpdatedAt)
		.Take(10)
		.ToArray()
		.Select(x => new ShelfItem
		{
			Type = "movie",
			Id = x.Id,
			EpisodeId = x.Episodes.FirstOrDefault()?.Id,
			VideoId = x.Episodes.FirstOrDefault()?.Videos.FirstOrDefault()?.Id,
			Title = x.TranslatedTitle.GetLocalized(x.Title, HttpContext)!,
			Subtitle = [x.ReleasedAt.Year.ToString()],
			Image = x.Poster
		});

	[HttpGet("recentContent/tv")]
	public IEnumerable<ShelfItem> RecentlyAddedTv(int? cutoffDays = null) => db.Content
		.Where(x => x.Type == DatabaseContent.ContentType.Tv)
		.OrderByDescending(x => x.UpdatedAt)
		.Take(10)
		.ToArray()
		.Select(x => new ShelfItem
		{
			Type = "tv",
			Id = x.Id,
			Title = x.TranslatedTitle.GetLocalized(x.Title, HttpContext)!,
			Subtitle = [x.ReleasedAt.Year.ToString()],
			Image = x.Poster
		});
}