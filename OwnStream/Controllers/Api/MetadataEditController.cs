using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Metadata;
using OwnStream.Plugin;
using OwnStream.PluginCore.ResultTypes;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/metadata"), EnableCors("Api"), Authorize(Roles = nameof(UserPermissions.WriteContent))]
public class MetadataEditController(DatabaseContext db, MetadataManager mm, PluginManager pm) : Controller
{
	[HttpGet("content/{id:guid}")]
	public ContentMetadataResponse? Get(Guid id)
	{
		DatabaseContent? content = db.Content.Find(id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseContentExternalId[] externalIds = db.ContentExternalIds.Where(x => x.ContentId == content.Id).ToArray();
		return new ContentMetadataResponse(content, externalIds);
	}

	[HttpPost("content/{id:guid}/refresh")]
	public async Task<ContentMetadataResponse?> RefreshContent(Guid id, bool apply = true)
	{
		DatabaseContent? content = db.Content.Include(x => x.ExternalIds).FirstOrDefault(x => x.Id == id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseContent refreshed = await mm.RefreshMetadataOf(content);
		if (apply) await db.SaveChangesAsync();
		return new ContentMetadataResponse(refreshed, refreshed.ExternalIds.ToArray());
	}

	[HttpPatch("content/{id:guid}")]
	public SuccessResponse<ContentMetadataResponse> Patch(Guid id, [FromBody] ContentMetadataEditRequest body)
	{
		DatabaseContent? content = db.Content.Find(id);
		DatabaseContentExternalId[] externalIds = db.ContentExternalIds.Where(x => x.ContentId == id).ToArray();
		if (content == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<ContentMetadataResponse>(false, $"Content with ID '{id}' not found.");
		}

		if (body.Type != null) content.Type = body.Type.Value;
		if (body.Title != null) content.Title = body.Title;
		if (body.Tagline != null) content.Tagline = body.Tagline;
		if (body.Description != null) content.Description = body.Description;

		if (body.TranslatedTitle != null)
		{
			foreach ((string key, string? value) in body.TranslatedTitle)
			{
				if (string.IsNullOrEmpty(value))
					content.TranslatedTitle.Remove(key);
				else
					content.TranslatedTitle[key] = value;
			}

			db.Entry(content).Property(x => x.TranslatedTitle).IsModified = true;
		}

		if (body.TranslatedTagline != null)
		{
			foreach ((string key, string? value) in body.TranslatedTagline)
			{
				if (string.IsNullOrEmpty(value))
					content.TranslatedTagline.Remove(key);
				else
					content.TranslatedTagline[key] = value;
			}

			db.Entry(content).Property(x => x.TranslatedTagline).IsModified = true;
		}

		if (body.TranslatedDescription != null)
		{
			foreach ((string key, string? value) in body.TranslatedDescription)
			{
				if (string.IsNullOrEmpty(value))
					content.TranslatedDescription.Remove(key);
				else
					content.TranslatedDescription[key] = value;
			}

			db.Entry(content).Property(x => x.TranslatedDescription).IsModified = true;
		}

		if (body.Poster != null) content.Poster = body.Poster;
		if (body.Banner != null) content.Banner = body.Banner;
		if (body.Logo != null) content.Logo = body.Logo;
		if (body.Backdrop != null) content.Backdrop = body.Backdrop;
		if (body.Thumbnail != null) content.Thumbnail = body.Thumbnail;

		if (body.ReleasedAt != null) content.ReleasedAt = body.ReleasedAt.Value;
		if (body.FinishedStreamingAt != null) content.FinishedStreamingAt = body.FinishedStreamingAt;
		content.UpdatedAt = DateTimeOffset.UtcNow;

		if (body.AgeRatings != null)
		{
			foreach (KeyValuePair<string, string?> kvp in body.AgeRatings)
			{
				if (string.IsNullOrEmpty(kvp.Value))
					content.AgeRatings.Remove(kvp.Key);
				else
					content.AgeRatings[kvp.Key] = kvp.Value;
			}

			db.Entry(content).Property(x => x.AgeRatings).IsModified = true;
		}

		if (body.ExternalIds != null)
		{
			foreach ((string key, string? value) in body.ExternalIds)
			{
				DatabaseContentExternalId? existing = externalIds.FirstOrDefault(x => x.ProviderId == key);
				if (string.IsNullOrEmpty(value))
				{
					if (existing != null)
						db.ContentExternalIds.Remove(existing);
				}
				else
				{
					if (existing != null)
					{
						existing.ExternalId = value;
					}
					else
					{
						db.ContentExternalIds.Add(new DatabaseContentExternalId
						{
							ContentId = content.Id,
							ProviderId = key,
							ExternalId = value
						});
					}
				}
			}
		}

		externalIds = db.ContentExternalIds.Where(x => x.ContentId == content.Id).ToArray();
		db.SaveChanges();

		return new SuccessResponse<ContentMetadataResponse>(true, "Content updated successfully.",
			new ContentMetadataResponse(content, externalIds));
	}

	[HttpGet("content/{id:guid}/seasons")]
	public IEnumerable<SeasonMetadataResponse>? GetSeasons(Guid id)
	{
		int[] available = db.Episode.Where(x => x.ParentContentId == id).Select(x => x.Season).Distinct().ToArray();
		DatabaseContent? content = db.Content.Find(id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		Dictionary<int, SeasonMetadataResponse> map = db.Seasons
			.Where(x => x.ContentId == id)
			.ToDictionary(x => x.Index, x => new SeasonMetadataResponse(x));
		return available.Select(i => map.GetValueOrDefault(i, new SeasonMetadataResponse(new DatabaseSeason
		{
			ContentId = id,
			Index = i,
			Title = $"Season {i}",
			TranslatedTitle = [],
			Description = "",
			TranslatedDescription = [],
			Poster = null,
			CreatedAt = default,
			UpdatedAt = default
		})));
	}

	[HttpGet("content/{id:guid}/seasons/{index:int}")]
	public SeasonMetadataResponse? GetSeasons(Guid id, int index)
	{
		DatabaseSeason? dbSeason = db.Seasons.FirstOrDefault(x => x.ContentId == id && x.Index == index);
		if (dbSeason == null && db.Episode.Any(x => x.ParentContentId == id && x.Season == index))
		{
			dbSeason = new DatabaseSeason
			{
				ContentId = id,
				Index = index,
				Title = $"Season {index}",
				TranslatedTitle = [],
				Description = "",
				TranslatedDescription = [],
				Poster = null,
				CreatedAt = default,
				UpdatedAt = default
			};
		}
		else if (dbSeason == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new SeasonMetadataResponse(dbSeason);
	}

	[HttpPost("content/{id:guid}/seasons/{index:int}/refresh")]
	public async Task<SeasonMetadataResponse?> GetSeasons(Guid id, int index, bool apply = true)
	{
		DatabaseSeason? season = db.Seasons.FirstOrDefault(x => x.ContentId == id && x.Index == index);
		Dictionary<string, string> externalIds = db.ContentExternalIds.Where(x => x.ContentId == id)
			.ToDictionary(x => x.ProviderId, x => x.ExternalId);

		if (season == null && db.Episode.Any(x => x.ParentContentId == id && x.Season == index))
		{
			season = new DatabaseSeason
			{
				ContentId = id,
				Index = index,
				Title = "",
				TranslatedTitle = [],
				Description = "",
				TranslatedDescription = [],
				Poster = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};
			db.Seasons.Add(season);
		}
		else if (season == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseSeason refreshed = await mm.RefreshMetadataOf(season, externalIds);
		if (apply) await db.SaveChangesAsync();
		return new SeasonMetadataResponse(refreshed);
	}

	[HttpPatch("content/{id:guid}/seasons/{index:int}")]
	public SuccessResponse<SeasonMetadataResponse> Patch(Guid id, int index, [FromBody] SeasonMetadataEditRequest body)
	{
		DatabaseSeason? season = db.Seasons.FirstOrDefault(x => x.ContentId == id && x.Index == index);
		Dictionary<string, string> externalIds = db.ContentExternalIds.Where(x => x.ContentId == id)
			.ToDictionary(x => x.ProviderId, x => x.ExternalId);

		if (season == null && db.Episode.Any(x => x.ParentContentId == id && x.Season == index))
		{
			season = new DatabaseSeason
			{
				ContentId = id,
				Index = index,
				Title = "",
				TranslatedTitle = [],
				Description = "",
				TranslatedDescription = [],
				Poster = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};
			db.Seasons.Add(season);
		}
		else if (season == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<SeasonMetadataResponse>(false, $"Season {index} for content '{id}' not found");
		}

		if (body.Title != null) season.Title = body.Title;
		if (body.Description != null) season.Description = body.Description;

		if (body.TranslatedTitle != null)
		{
			foreach ((string key, string? value) in body.TranslatedTitle)
			{
				if (string.IsNullOrEmpty(value))
					season.TranslatedTitle.Remove(key);
				else
					season.TranslatedTitle[key] = value;
			}

			db.Entry(season).Property(x => x.TranslatedTitle).IsModified = true;
		}

		if (body.TranslatedDescription != null)
		{
			foreach ((string key, string? value) in body.TranslatedDescription)
			{
				if (string.IsNullOrEmpty(value))
					season.TranslatedDescription.Remove(key);
				else
					season.TranslatedDescription[key] = value;
			}

			db.Entry(season).Property(x => x.TranslatedDescription).IsModified = true;
		}

		if (body.Poster != null) season.Poster = body.Poster;

		season.UpdatedAt = DateTimeOffset.UtcNow;
		db.SaveChanges();

		return new SuccessResponse<SeasonMetadataResponse>(true, "Season updated successfully.",
			new SeasonMetadataResponse(season));
	}

	[HttpGet("content/{id:guid}/episodes")]
	public IEnumerable<EpisodeMetadataResponse>? GetEpisodes(Guid id)
	{
		DatabaseContent? content = db.Content.Find(id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseEpisode[] episodes = db.Episode.Where(x => x.ParentContentId == content.Id)
			.OrderBy(x => x.Season)
			.ThenBy(x => x.Episode)
			.ToArray();

		return episodes.Select(x => new EpisodeMetadataResponse(x));
	}

	[HttpGet("episode/{id:guid}")]
	public EpisodeMetadataResponse? GetEpisode(Guid id)
	{
		DatabaseEpisode? episode = db.Episode.Find(id);
		if (episode == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new EpisodeMetadataResponse(episode);
	}

	[HttpPost("episode/{id:guid}/refresh")]
	public async Task<EpisodeMetadataResponse?> RefreshEpisode(Guid id, bool apply = true)
	{
		DatabaseEpisode? content = db.Episode.Include(x => x.ParentContent).ThenInclude(x => x.ExternalIds)
			.FirstOrDefault(x => x.Id == id);
		if (content == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseEpisode refreshed = await mm.RefreshMetadataOf(content);
		if (apply) await db.SaveChangesAsync();
		return new EpisodeMetadataResponse(refreshed);
	}

	[HttpPatch("episode/{id:guid}")]
	public SuccessResponse<EpisodeMetadataResponse> PatchEpisode(Guid id, [FromBody] EpisodeMetadataEditRequest body)
	{
		DatabaseEpisode? episode = db.Episode.Find(id);
		if (episode == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<EpisodeMetadataResponse>(false, $"Episode with ID '{id}' not found.");
		}

		if (body.Season != null) episode.Season = body.Season.Value;
		if (body.Episode != null) episode.Episode = body.Episode.Value;

		if (body.Title != null) episode.Title = body.Title;
		if (body.Summary != null) episode.Summary = body.Summary;

		if (body.TranslatedTitle != null)
		{
			foreach ((string key, string? value) in body.TranslatedTitle)
			{
				if (string.IsNullOrEmpty(value))
					episode.TranslatedTitle.Remove(key);
				else
					episode.TranslatedTitle[key] = value;
			}

			db.Entry(episode).Property(x => x.TranslatedTitle).IsModified = true;
		}

		if (body.TranslatedSummary != null)
		{
			foreach ((string key, string? value) in body.TranslatedSummary)
			{
				if (string.IsNullOrEmpty(value))
					episode.TranslatedSummary.Remove(key);
				else
					episode.TranslatedSummary[key] = value;
			}

			db.Entry(episode).Property(x => x.TranslatedSummary).IsModified = true;
		}

		if (body.Thumbnail != null) episode.Thumbnail = body.Thumbnail;
		if (body.ReleasedAt != null) episode.ReleasedAt = body.ReleasedAt.Value;
		episode.UpdatedAt = DateTimeOffset.UtcNow;
		db.SaveChanges();

		return new SuccessResponse<EpisodeMetadataResponse>(true, "Episode updated successfully.",
			new EpisodeMetadataResponse(episode));
	}

	[HttpGet("search/{providerId}")]
	public async Task<SuccessResponse<IEnumerable<PluginContentSearchResult>>> SearchProviders(string providerId,
		[FromQuery] Guid contentId)
	{
		if (providerId.Contains('.'))
		{
			Response.StatusCode = 400;
			return new SuccessResponse<IEnumerable<PluginContentSearchResult>>(false, "Bad provider ID");
		}

		DatabaseContent? content = await db.Content.FindAsync(contentId, HttpContext.RequestAborted);
		if (content == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<IEnumerable<PluginContentSearchResult>>(false, "Content not found");
		}

		try
		{
			IEnumerable<MetadataSearchResult[]> results =
				await pm.InvokeForPluginsWithPermission<MetadataSearchResult[]>(
					$"metadata.provide.{providerId}",
					"metadata.search",
					content.Type.ToString(),
					content.Title,
					content.ReleasedAt.Year
				);
			return new SuccessResponse<IEnumerable<PluginContentSearchResult>>(true,
				$"Searched for {content.Title} ({content.ReleasedAt.Year}) under {content.Type.ToString()}",
				results.SelectMany(l => l.Select(x => new PluginContentSearchResult(x))));
		}
		catch (Exception e)
		{
			Response.StatusCode = 500;
			return new SuccessResponse<IEnumerable<PluginContentSearchResult>>(false, e.Message);
		}
	}
}