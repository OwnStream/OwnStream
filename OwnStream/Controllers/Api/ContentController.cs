using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/content/"), Authorize, EnableCors("Api")]
public class ContentController(DatabaseContext db) : Controller
{
	[HttpGet("library")]
	public IQueryable<Library> GetLibraries() => db.Libraries.Select(x => new Library(x)
	{
		Path = $"/{x.Id}",
		DiskUsage = null
	});

	[HttpGet("library/{id:guid}")]
	public PagedResponse<Content> GetLibraryItems(Guid id, DatabaseContent.ContentType? typeFilter = null, int page = 0,
		int limit = 40)
	{
		IQueryable<DatabaseContent> query = db.Content.AsQueryable();
		if (id != Guid.Empty)
			query = query.Where(x => x.LibraryId == id);
		if (typeFilter != null)
			query = query.Where(x => x.Type == typeFilter);

		int count = query.Count();
		int pages = (int)Math.Ceiling(count / (float)limit);
		
		return new PagedResponse<Content>
		{
			Items = query
				.Skip(page * limit)
				.Take(limit)
				.ToArray()
				.Select(x => new Content(x, HttpContext)),
			HasMore = pages > page + 1,
			Count = count,
			Pages = pages
		};
	}

	[HttpGet("{id:guid}/details")]
	public Content? GetDetails(Guid id)
	{
		DatabaseContent? content = db.Content
			.Include(x => x.Episodes)
			.ThenInclude(x => x.Videos)
			.FirstOrDefault(x => x.Id == id);

		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new Content(content, HttpContext);
	}

	[HttpGet("{id:guid}/seasons")]
	public Season[]? GetSeasons(Guid id)
	{
		DatabaseContent? content = db.Content
			.Include(x => x.Episodes)
			.FirstOrDefault(x => x.Id == id);

		if (content == null)
		{
			Response.StatusCode = 404;
			return [];
		}

		return content.Episodes
			.GroupBy(x => x.Season)
			.Select(x => new Season
			{
				Index = x.Key,
				EpisodeCount = x.Count(),
			})
			.OrderBy(x => x.Index)
			.ToArray();
	}

	[HttpGet("{id:guid}/seasons/{season:int}/episodes")]
	public Episode[]? GetEpisodes(Guid id, int season)
	{
		DatabaseContent? content = db.Content
			.Include(x => x.Episodes)
			.ThenInclude(x => x.Videos)
			.Include(x => x.Episodes)
			.ThenInclude(x => x.WatchProgresses)
			.FirstOrDefault(x => x.Id == id);

		if (content == null)
		{
			Response.StatusCode = 404;
			return [];
		}

		return content.Episodes
			.Where(x => x.Season == season)
			.Select(x => new Episode(x, HttpContext))
			.OrderBy(x => x.EpisodeNumber)
			.ToArray();
	}

	[HttpGet("episode/{id:guid}")]
	public Episode? GetEpisode(Guid id)
	{
		DatabaseEpisode? content = db.Episode
			.Include(x => x.Videos)
			.Include(x => x.WatchProgresses)
			.FirstOrDefault(x => x.Id == id);

		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new Episode(content, HttpContext);
	}

	[HttpGet("episode/{id:guid}/next")]
	public Episode? GetNextEpisode(Guid id)
	{
		DatabaseEpisode? episode = db.Episode
			                           .Include(x => x.Videos)
			                           .Include(x => x.WatchProgresses)
			                           .FirstOrDefault(x => x.Id == id) ??
		                           db.Videos
			                           .Include(x => x.Episode)
			                           .ThenInclude(x => x!.Videos)
			                           .Include(x => x.Episode)
			                           .ThenInclude(x => x!.WatchProgresses)
			                           .FirstOrDefault(x => x.Id == id)?
			                           .Episode;

		if (episode == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseEpisode? nextEpisode = db.Episode
			.Include(x => x.Videos)
			.Where(x => x.ParentContentId == episode.ParentContentId)
			.Where(x => x.Season > episode.Season ||
			            (x.Season == episode.Season && x.Episode > episode.Episode))
			.OrderBy(x => x.Season)
			.ThenBy(x => x.Episode)
			.FirstOrDefault();

		return nextEpisode == null ? null : new Episode(nextEpisode, HttpContext);
	}
}