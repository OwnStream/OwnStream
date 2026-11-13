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

	[HttpPost("test")]
	public async Task<SuccessResponse<IEnumerable<MetadataSearchResult>>> Test(
		[FromBody] Dictionary<string, string> args)
	{
		string query = args["query"]!;
		string year = args["year"]!;
		string type = args["type"]!;

		try
		{
			IEnumerable<MetadataSearchResult[]> res =
				await manager.InvokeForPluginsWithFeature<MetadataSearchResult[]>("metadata.search", type, query, int.Parse(year));
			return new SuccessResponse<IEnumerable<MetadataSearchResult>>(true, "Plugin invoked successfully",
				res!.SelectMany(x => x ?? []));
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			return new SuccessResponse<IEnumerable<MetadataSearchResult>>(false, e.Message);
		}
	}
}