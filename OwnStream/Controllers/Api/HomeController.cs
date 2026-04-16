using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/home"), Authorize(AuthenticationSchemes = "ApiToken")]
public class HomeController(DatabaseContext db) : Controller
{
	[HttpGet("shelves")]
	public IEnumerable<Shelf> GetShelves()
	{
		List<Shelf> shelves = [];

		var cutoff = DateTimeOffset.UtcNow.AddDays(30);
		shelves.Add(new Shelf
		{
			Title = "Recently Added",
			Type = "recent",
			Items = db.Episode
				.Include(x => x.ParentContent)
				.Include(x => x.Videos)
				.Where(x => x.UpdatedAt < cutoff)
				.OrderByDescending(x => x.UpdatedAt)
				.Take(50)
				.ToArray()
				.GroupBy(x => x.ParentContentId)
				.Select(x => x.FirstOrDefault())
				.Where(x => x != null)
				.Cast<DatabaseEpisode>()
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
					VideoId = x.Videos.First().Id,
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
					}
				})
		});
		shelves.Add(new Shelf
		{
			Title = "Movies",
			Type = "movie",
			Items = db.Content
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
					VideoId = x.Episodes.FirstOrDefault()?.Videos?.FirstOrDefault()?.Id,
					Title = x.TranslatedTitle.GetLocalized(x.Title, HttpContext)!,
					Subtitle = [x.ReleasedAt.Year.ToString()],
					Image = x.Poster
				})
		});
		shelves.Add(new Shelf
		{
			Title = "Shows",
			Type = "tv",
			Items = db.Content
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
				})
		});
		return shelves;
	}
}