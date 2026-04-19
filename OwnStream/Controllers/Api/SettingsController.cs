using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;


[ApiController, Route("/api/settings/")]
public class SettingsController(DatabaseContext db, Configuration config) : Controller
{
	[HttpGet("get"), Authorize(Roles = nameof(UserPermissions.ReadSettings), AuthenticationSchemes = "ApiToken"),
	 EnableCors("Api")]
	public IActionResult Get()
	{
		return Json(config);	
	}
	[HttpPost("update"), Authorize(Roles = nameof(UserPermissions.WriteSettings), AuthenticationSchemes = "ApiToken"),
	 EnableCors("Api")]
	public IActionResult Update([FromBody] Configuration newConfig)
	{
		config.Transcode = newConfig.Transcode;
		config.SaveConfiguration();
		return Json(config);	
	}
}