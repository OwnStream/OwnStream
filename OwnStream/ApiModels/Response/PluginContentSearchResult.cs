using OwnStream.PluginCore.ResultTypes;

namespace OwnStream.ApiModels.Response;

public class PluginContentSearchResult(MetadataSearchResult result)
{
	public string PluginId { get; } = result.Id.Split(':')[0];
	public string ExternalId { get; } = result.Id.Split(':')[1];
	public string[] Titles { get; } = result.Titles;
	public string? Poster { get; } = result.Poster;
	public int? Year = result.Year;
}