using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/metadata"), EnableCors("Api"), Authorize(Roles = nameof(UserPermissions.WriteContent))]
public class MetadataEditController(DatabaseContext db) : Controller
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
}