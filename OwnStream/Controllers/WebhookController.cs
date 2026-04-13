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
	[Authorize]
	public IActionResult Index() => View(db.Webhooks.ToArray());

	[Authorize]
	public IActionResult Detail(Guid id)
	{
		DatabaseWebhook? webhook = db.Webhooks.Find(id);
		if (webhook == null) return RedirectToAction(nameof(Index));
		return View(webhook);
	}

	[Authorize, HttpGet]
	public IActionResult Create() => View(db.Libraries.ToArray());

	[Authorize, HttpPost]
	public async Task<IActionResult> Create([FromForm] string? name, [FromForm] string? authentication,
		[FromForm] bool deleteOnConvert, [FromForm] Guid libraryId)
	{
		if (name == null) return BadRequest("Name cannot be empty");
		DatabaseLibrary? library = db.Libraries.Find(libraryId);
		if (library == null) return BadRequest("Library cannot be found");

		DatabaseWebhook webhook = new()
		{
			Id = Guid.NewGuid(),
			LibraryId = libraryId,
			Name = name,
			Authentication = authentication ?? "",
			DeleteOnConvert = deleteOnConvert,
		};
		db.Add(webhook);
		await db.SaveChangesAsync();

		return RedirectToAction(nameof(Detail), new { id = webhook.Id });
	}

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
				// TODO: Get from settings
				Resolutions =
				[
					new TranscodeJob.Arguments.ResolutionInfo
						{ Name = "360p", Width = 640, Bitrate = 3000000, Codec = "h264_nvenc" },
					new TranscodeJob.Arguments.ResolutionInfo
						{ Name = "720p", Width = 1280, Bitrate = 7000000, Codec = "h264_nvenc" },
					new TranscodeJob.Arguments.ResolutionInfo
						{ Name = "1080p", Width = 1920, Bitrate = 15000000, Codec = "hevc_nvenc" }
				],
				AudioResolutions =
				[
					new TranscodeJob.Arguments.AudioResolutionInfo { Bitrate = 128000, Channels = 2, Codec = "aac" }
				],
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
}