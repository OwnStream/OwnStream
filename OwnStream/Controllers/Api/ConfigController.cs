using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/config"), EnableCors("Api"), Authorize]
public class ConfigController(DatabaseContext db) : Controller
{
	[HttpGet("user")]
	public Dictionary<string, string?> GetUserConfig([FromQuery] string? prefix) => HandleGet(
		$"User.{Guid.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value)}", prefix);

	[HttpPatch("user")]
	public SuccessResponse PatchUserConfig([FromBody] Dictionary<string, string?> values)
	{
		HandlePatch($"User.{Guid.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value)}", values);
		return new SuccessResponse(true);
	}

	[HttpGet("server")]
	public Dictionary<string, string?> GetServerConfig([FromQuery] string? prefix) => HandleGet("Server", prefix);

	[HttpPatch("server")]
	public SuccessResponse PatchServerConfig([FromBody] Dictionary<string, string?> values)
	{
		HandlePatch($"Server", values);
		return new SuccessResponse(true);
	}

	private Dictionary<string, string?> HandleGet(string realm, string? prefix)
	{
		IQueryable<DatabaseConfigEntry> query = db.ConfigEntries.Where(x => x.Realm == realm);
		if (!string.IsNullOrWhiteSpace(prefix))
			query = query.Where(x => x.Key.StartsWith(prefix));

		return query.ToDictionary(x => x.Key, x => x.Value);
	}

	private void HandlePatch(string realm, Dictionary<string, string?> values)
	{
		foreach ((string key, string? value) in values)
		{
			DatabaseConfigEntry? entry = db.ConfigEntries.FirstOrDefault(x => x.Realm == realm && x.Key == key);
			if (value == null && entry != null)
				db.Remove(entry);
			else if (value != null && entry != null)
				entry.Value = value;
			else if (value != null && entry == null)
			{
				entry = new DatabaseConfigEntry
				{
					Realm = realm,
					Key = key,
					Value = value
				};
				db.ConfigEntries.Add(entry);
			}
		}

		db.SaveChanges();
	}
}