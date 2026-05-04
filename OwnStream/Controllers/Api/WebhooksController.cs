using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/manage/webhooks/"), EnableCors("Api"),
 Authorize(Roles = nameof(UserPermissions.ReadWebhooks), AuthenticationSchemes = "ApiToken")]
public class WebhooksController(DatabaseContext db) : Controller
{
	[HttpGet("list")]
	public IEnumerable<Webhook> ListLibraries() => db.Webhooks.Select(x => new Webhook(x));

	[HttpGet("{id:guid}")]
	public Webhook? GetWebhook(Guid id)
	{
		DatabaseWebhook? webhook = db.Webhooks.Find(id);
		if (webhook == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new Webhook(webhook);
	}

	[HttpPatch("{id:guid}")]
	public SuccessResponse<Webhook> PatchWebhook(Guid id, [FromBody] PatchWebhookRequest request)
	{
		DatabaseWebhook? webhook = db.Webhooks.Find(id);
		if (webhook == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse<Webhook>(false, "Webhook not found");
		}

		if (request.Name != null) webhook.Name = request.Name;
		if (request.Authentication != null) webhook.Authentication = request.Authentication;
		if (request.DeleteOnConvert.HasValue) webhook.DeleteOnConvert = request.DeleteOnConvert.Value;
		if (request.LibraryId != null)
		{
			if (Guid.TryParse(request.LibraryId, out Guid libraryId))
			{
				DatabaseLibrary? library = db.Libraries.Find(libraryId);

				if (library == null)
				{
					Response.StatusCode = 404;
					return new SuccessResponse<Webhook>(false, "Library cannot be found");
				}

				webhook.LibraryId = libraryId;
			}
			else
			{
				Response.StatusCode = 400;
				return new SuccessResponse<Webhook>(false, "Library ID is not a valid UUID");
			}
		}

		db.SaveChanges();
		return new SuccessResponse<Webhook>(true, null, new Webhook(webhook));
	}

	[HttpDelete("{id:guid}")]
	public SuccessResponse DeleteWebhook(Guid id)
	{
		DatabaseWebhook? webhook = db.Webhooks.Find(id);
		if (webhook == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, "Webhook not found");
		}

		db.Webhooks.Remove(webhook);
		db.SaveChanges();
		return new SuccessResponse(true);
	}

	[HttpPost("new")]
	public SuccessResponse<Webhook> CreateWebhook([FromBody] CreateWebhookRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			return new SuccessResponse<Webhook>(false, "Name cannot be empty");
		if (!Guid.TryParse(request.LibraryId, out Guid libraryId))
			return new SuccessResponse<Webhook>(false, "Library ID is not a valid UUID");

		DatabaseLibrary? library = db.Libraries.Find(libraryId);

		if (library == null)
			return new SuccessResponse<Webhook>(false, "Library cannot be found");

		DatabaseWebhook webhook = new()
		{
			Id = Guid.NewGuid(),
			LibraryId = libraryId,
			Name = request.Name,
			Authentication = request.Authentication,
			DeleteOnConvert = request.DeleteOnConvert
		};
		db.Add(webhook);
		db.SaveChanges();

		return new SuccessResponse<Webhook>(true, null, new Webhook(webhook));
	}
}