using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;

namespace OwnStream.Controllers.Api;

[ApiController]
public class InfoController(DatabaseContext db) : Controller
{
	[HttpGet("/api/info")]
	public IActionResult GetInfo()
	{
		return Json(new
		{
			type = "ownstream",
			name = "OwnStream", // TODO: Will be configurable
			version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
			setupComplete = db.IsSetup()
		});
	}
}