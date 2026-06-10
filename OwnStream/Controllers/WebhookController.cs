using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;
using OwnStream.Services;

namespace OwnStream.Controllers;

public class WebhookController(
	ILogger<WebhookController> logger,
	DatabaseContext db,
	IFfmpegJobQueueService queueService) : Controller
{
	[Route("/api/webhook/{id:guid}/radarr")]
	public IActionResult HandleRadarr([FromRoute] Guid id, [FromBody] RadarrWebhookBody body)
	{
		DatabaseWebhook? webhook = db.Webhooks.Include(x => x.Library).FirstOrDefault(x => x.Id == id);
		if (webhook == null) return NotFound();

		if (webhook.Authentication.Length > 0)
		{
			string? header = Request.Headers.Authorization.FirstOrDefault();
			if (header != webhook.Authentication)
				return Unauthorized();
		}

		if (body.Type == "Test")
		{
			logger.LogInformation("Test webhook received from {Name}", webhook.Name);
			return Ok();
		}

		if (body.Type != "Download")
		{
			logger.LogWarning(
				"Unexpected webhook type '{Type}' received from webhook {Name}. Please only enable the 'On File Import' and 'On File Upgrade' toggles in Radarr settings.",
				body.Type, webhook.Name);
			return BadRequest();
		}

		logger.LogInformation(
			"Webhook received from {Name}, will import file at {Path} with metadata provider IDs imdbId={ImdbId}, tmdbId={TmdbId}",
			webhook.Name, body.MovieFile.Path, body.RemoteMovie.ImdbId, body.RemoteMovie.TmdbId);

		Guid videoId = Guid.NewGuid();
		queueService.EnqueueAsync("TranscodeFull", body.MovieFile.Path,
			Path.Join(webhook.Library.Path, videoId.ToString()),
			new TranscodeJob.Arguments
			{
				VideoId = videoId,
				LibraryId = webhook.LibraryId,
				DeleteAfterTranscode = webhook.DeleteOnConvert,
				Metadata = new Dictionary<string, string>()
				{
					["type"] = "movie",
					["season"] = "1",
					["episode"] = "1",
					["imdbid"] = body.RemoteMovie.ImdbId,
					["tmdbid"] = body.RemoteMovie.TmdbId.ToString()
				}
			});

		return Ok();
	}

	[Route("/api/webhook/{id:guid}/sonarr")]
	public async Task<IActionResult> HandleSonarr([FromRoute] Guid id, [FromBody] SonarrWebhookBody body)
	{
		DatabaseWebhook? webhook = db.Webhooks.Include(x => x.Library).FirstOrDefault(x => x.Id == id);
		if (webhook == null) return NotFound();

		if (webhook.Authentication.Length > 0)
		{
			string? header = Request.Headers.Authorization.FirstOrDefault();
			if (header != webhook.Authentication)
				return Unauthorized();
		}

		if (body.Type == "Test")
		{
			logger.LogInformation("Test webhook received from {Name}", webhook.Name);
			return Ok();
		}

		if (body.Type != "Download")
		{
			logger.LogWarning(
				"Unexpected webhook type '{Type}' received from webhook {Name}. Please only enable the 'On File Import' and 'On File Upgrade' toggles in Radarr settings.",
				body.Type, webhook.Name);
			return BadRequest();
		}

		logger.LogInformation(
			"Webhook received from {Name}, will import file at {Path} with metadata provider IDs season={Season} episode={Episode} tvdbId={TvdbId} imdbId={ImdbId}, tmdbId={TmdbId}",
			webhook.Name, body.EpisodeFile.Path, body.Episodes[0].SeasonNumber, body.Episodes[0].EpisodeNumber,
			body.Series.TvdbId, body.Series.ImdbId, body.Series.TmdbId);

		Guid videoId = Guid.NewGuid();
		DatabaseContent? relevantContent = db.Content
			.Include(x => x.Episodes)
			.Include(x => x.ExternalIds)
			.FirstOrDefault(x =>
				x.ExternalIds.Any(c => c.ProviderId == "imdb" && c.ExternalId == body.Series.ImdbId) &&
				x.ExternalIds.Any(c => c.ProviderId == "tmdb" && c.ExternalId == body.Series.TmdbId.ToString()) &&
				x.ExternalIds.Any(c => c.ProviderId == "tvMaze" && c.ExternalId == body.Series.TvMazeId.ToString()) &&
				x.LibraryId == webhook.LibraryId);
		DatabaseFfmpegJob job = await queueService.EnqueueAsync("TranscodeFull", body.EpisodeFile.Path,
			Path.Join(webhook.Library.Path, videoId.ToString()),
			new TranscodeJob.Arguments
			{
				VideoId = videoId,
				LibraryId = webhook.LibraryId,
				DeleteAfterTranscode = webhook.DeleteOnConvert,
				Metadata = new Dictionary<string, string>()
				{
					["type"] = "tv",
					["season"] = body.Episodes[0].SeasonNumber.ToString(),
					["episode"] = body.Episodes[0].EpisodeNumber.ToString(),
					["tvdbid"] = body.Series.TvdbId.ToString(),
					["tvMazeid"] = body.Series.TvMazeId.ToString(),
					["imdbid"] = body.Series.ImdbId,
					["tmdbid"] = body.Series.TmdbId.ToString()
				}
			},
			relevantVideoId: null, // video doesn't exist yet, cannot set it
			relevantWebhookId: webhook.Id,
			relevantLibraryId: webhook.LibraryId,
			relevantContentId: relevantContent?.Id,
			relevantEpisodeId: relevantContent?.Episodes.FirstOrDefault(x =>
				x.Season == body.Episodes[0].SeasonNumber && x.Episode == body.Episodes[0].EpisodeNumber)?.Id);

		return Ok(job.Id);
	}

	// Only the parts we need are defined.
	public class RadarrWebhookBody
	{
		[JsonPropertyName("eventType")] public string Type { get; set; }
		[JsonPropertyName("remoteMovie")] public RadarrRemoveMovie RemoteMovie { get; set; }
		[JsonPropertyName("movieFile")] public RadarrMovieFile MovieFile { get; set; }

		public class RadarrRemoveMovie
		{
			[JsonPropertyName("tmdbId")] public int TmdbId { get; set; }
			[JsonPropertyName("imdbId")] public string ImdbId { get; set; }
			[JsonPropertyName("title")] public string Title { get; set; }
			[JsonPropertyName("year")] public int Year { get; set; }
		}

		public class RadarrMovieFile
		{
			[JsonPropertyName("path")] public string Path { get; set; }
		}
	}

	public class SonarrWebhookBody
	{
		[JsonPropertyName("eventType")] public string Type { get; set; }
		[JsonPropertyName("series")] public SonarrSeriesInfo Series { get; set; }
		[JsonPropertyName("episodes")] public SonarrEpisodeInfo[] Episodes { get; set; }
		[JsonPropertyName("episodeFile")] public SonarrEpisodeFile EpisodeFile { get; set; }

		public class SonarrSeriesInfo
		{
			[JsonPropertyName("tvdbId")] public int TvdbId { get; set; }
			[JsonPropertyName("tvMazeId")] public int TvMazeId { get; set; }
			[JsonPropertyName("tmdbId")] public int TmdbId { get; set; }
			[JsonPropertyName("imdbId")] public string ImdbId { get; set; }
			[JsonPropertyName("title")] public string Title { get; set; }
			[JsonPropertyName("year")] public int Year { get; set; }
		}

		public class SonarrEpisodeInfo
		{
			[JsonPropertyName("tvdbId")] public int TvdbId { get; set; }
			[JsonPropertyName("seasonNumber")] public int SeasonNumber { get; set; }
			[JsonPropertyName("episodeNumber")] public int EpisodeNumber { get; set; }
		}

		public class SonarrEpisodeFile
		{
			[JsonPropertyName("path")] public string Path { get; set; }
		}
	}
}