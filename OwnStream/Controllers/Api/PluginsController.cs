using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Response;
using OwnStream.Database.Models;
using OwnStream.Plugin;
using OwnStream.PluginCore;
using OwnStream.PluginCore.ResultTypes;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/plugins/"), EnableCors("Api"), Authorize(Roles = nameof(UserPermissions.Admin))]
public class PluginsController(PluginManager manager) : Controller
{
	[HttpGet("list")]
	public IEnumerable<PluginItem> ListPlugins() => manager.GetAvailablePlugins().Select(x => new PluginItem(x));

	[HttpGet("enable")]
	public SuccessResponse EnablePlugin(string path)
	{
		try
		{
			manager.EnablePlugin(path);
			return new SuccessResponse(true, "");
		}
		catch (Exception e)
		{
			return new SuccessResponse(false, e.Message);
		}
	}

	[HttpGet("disable")]
	public SuccessResponse DisablePlugin(string path)
	{
		try
		{
			manager.DisablePlugin(path);
			return new SuccessResponse(true, "");
		}
		catch (Exception e)
		{
			return new SuccessResponse(false, e.Message);
		}
	}

	[HttpPost("install")]
	public async Task<SuccessResponse<PluginMeta>> Install(IFormFile file)
	{
		try
		{
			await using Stream fs = file.OpenReadStream();
			PluginMeta meta = await manager.InstallPlugin(fs);
			return new SuccessResponse<PluginMeta>(true, null, meta);
		}
		catch (Exception e)
		{
			return new SuccessResponse<PluginMeta>(false, e.Message);
		}
	}

	[HttpPost("delete")]
	public SuccessResponse Delete([FromQuery] string path)
	{
		try
		{
			manager.DeletePlugin(path);
			return new SuccessResponse(true);
		}
		catch (Exception e)
		{
			return new SuccessResponse(false, e.Message);
		}
	}

	[HttpPost("reload")]
	public SuccessResponse Reload([FromQuery] string path)
	{
		try
		{
			manager.ReloadPlugin(path);
			return new SuccessResponse(true);
		}
		catch (Exception e)
		{
			return new SuccessResponse(false, e.Message);
		}
	}
}