using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

public class WebhookController(ILogger<WebhookController> logger, DatabaseContext db) : Controller
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
		DatabaseWebhook? webhook = db.Webhooks.Find(id);
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