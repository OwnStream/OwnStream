using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Plugin;
using OwnStream.PluginCore.ResultTypes;

namespace OwnStream.Metadata;

public class MetadataManager(DatabaseContext db, PluginManager pm, Configuration config)
{
	public async Task<DatabaseContent> RefreshMetadataOf(DatabaseContent content, Dictionary<string, string>? extraExternalIds = null)
	{
		Dictionary<string, string> externalIds = JoinExternalIds(content.ExternalIds, extraExternalIds);
		switch (content.Type)
		{
			case DatabaseContent.ContentType.Movie:
				await RefreshMovieMetadata(content, externalIds);
				break;
			case DatabaseContent.ContentType.Tv:
				await RefreshTvMetadata(content, externalIds);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unexpected content type: {content.Type}");
		}

		foreach ((string provider, string id) in externalIds) SetExternalId(content, provider, id);
		return content;
	}

	public async Task<DatabaseEpisode> RefreshMetadataOf(DatabaseEpisode episode, Dictionary<string, string>? extraExternalIds = null)
	{
		MetadataEpisodeResult[] orderedResults =
			await CallPlugins<MetadataEpisodeResult>(
				JoinExternalIds(episode.ParentContent.ExternalIds, extraExternalIds), "metadata.getEpisode",
				episode.Season, episode.Episode);

		episode.Title = orderedResults.FirstNonEmptyValue(x => x.Title) ?? episode.Title;
		episode.Summary = orderedResults.FirstNonEmptyValue(x => x.Summary) ?? episode.Summary;

		episode.TranslatedTitle = orderedResults
			.CombineDicts(x => x.TitleTranslations ?? [], episode.TranslatedTitle);
		episode.TranslatedSummary = orderedResults
			.CombineDicts(x => x.SummaryTranslations ?? [], episode.TranslatedSummary);

		episode.Thumbnail = orderedResults.CombineLists(x => x.Thumbnails ?? []).FirstOrDefault()?.Url ??
		                    episode.Thumbnail;

		episode.UpdatedAt = DateTimeOffset.UtcNow;
		episode.ReleasedAt = Utils.TryParseIsoString(orderedResults.FirstNonEmptyValue(x => x.ReleaseDate),
			out DateTimeOffset erDate)
			? erDate
			: episode.ReleasedAt;
		return episode;
	}

	private static Dictionary<string, string> JoinExternalIds(IEnumerable<DatabaseContentExternalId> dbIds,
		params Dictionary<string, string>?[] ids)
	{
		Dictionary<string, string> r = [];
		foreach (DatabaseContentExternalId id in dbIds)
			r[id.ProviderId] = id.ExternalId;
		foreach (Dictionary<string, string> dictionary in ids.Where(x => x != null).Cast<Dictionary<string, string>>())
		foreach ((string key, string value) in dictionary)
			r[key] = value;
		return r;
	}

	private async Task RefreshMovieMetadata(DatabaseContent content, Dictionary<string, string> externalIds)
	{
		MetadataMovieResult[] orderedResults = await CallPlugins<MetadataMovieResult>(externalIds, "metadata.getMovie");
		// TODO: add this to plugins
		/*
		foreach ((string providerId, string externalId) in args.ProviderIds)
			SetExternalId(content, providerId, externalId);
		*/
		content.Title = orderedResults.FirstNonEmptyValue(x => x.Title) ?? content.Title;
		content.Tagline = orderedResults.FirstNonEmptyValue(x => x.Tagline) ?? content.Tagline;
		content.Description = orderedResults.FirstNonEmptyValue(x => x.Description) ?? content.Description;

		content.TranslatedTitle = orderedResults.CombineDicts(x => x.TitleTranslations ?? [], content.TranslatedTitle);
		content.TranslatedTagline =
			orderedResults.CombineDicts(x => x.TaglineTranslations ?? [], content.TranslatedTagline);
		content.TranslatedDescription =
			orderedResults.CombineDicts(x => x.DescriptionTranslations ?? [], content.TranslatedDescription);

		content.Poster = orderedResults.CombineLists(x => x.Images?["poster"] ?? []).FirstOrDefault()?.Url ??
		                 content.Poster;
		content.Banner = orderedResults.CombineLists(x => x.Images?["banner"] ?? []).FirstOrDefault()?.Url ??
		                 content.Banner;
		content.Logo = orderedResults.CombineLists(x => x.Images?["logo"] ?? []).FirstOrDefault()?.Url ??
		               content.Logo;
		content.Backdrop = orderedResults.CombineLists(x => x.Images?["backdrop"] ?? []).FirstOrDefault()?.Url ??
		                   content.Backdrop;
		content.Thumbnail = orderedResults.CombineLists(x => x.Images?["thumbnail"] ?? []).FirstOrDefault()?.Url ??
		                    content.Thumbnail;

		content.AgeRatings = orderedResults.CombineDicts(x => x.AgeRatings ?? [], content.AgeRatings);

		content.ReleasedAt =
			Utils.TryParseIsoString(orderedResults.FirstNonEmptyValue(x => x.ReleaseDate), out DateTimeOffset rDate)
				? rDate
				: content.ReleasedAt;
		content.UpdatedAt = DateTimeOffset.UtcNow;
	}

	private async Task RefreshTvMetadata(DatabaseContent content, Dictionary<string, string> externalIds)
	{
		MetadataTvResult[] orderedResults = await CallPlugins<MetadataTvResult>(externalIds, "metadata.getTv");
		// TODO: add this to plugins
		/*
		foreach ((string providerId, string externalId) in args.ProviderIds)
			SetExternalId(content, providerId, externalId);
		*/
		content.Title = orderedResults.FirstNonEmptyValue(x => x.Title) ?? content.Title;
		content.Tagline = orderedResults.FirstNonEmptyValue(x => x.Tagline) ?? content.Tagline;
		content.Description = orderedResults.FirstNonEmptyValue(x => x.Description) ?? content.Description;

		content.TranslatedTitle = orderedResults.CombineDicts(x => x.TitleTranslations ?? [], content.TranslatedTitle);
		content.TranslatedTagline =
			orderedResults.CombineDicts(x => x.TaglineTranslations ?? [], content.TranslatedTagline);
		content.TranslatedDescription =
			orderedResults.CombineDicts(x => x.DescriptionTranslations ?? [], content.TranslatedDescription);

		content.Poster = orderedResults.CombineLists(x => x.Images?["poster"] ?? []).FirstOrDefault()?.Url ??
		                 content.Poster;
		content.Banner = orderedResults.CombineLists(x => x.Images?["banner"] ?? []).FirstOrDefault()?.Url ??
		                 content.Banner;
		content.Logo = orderedResults.CombineLists(x => x.Images?["logo"] ?? []).FirstOrDefault()?.Url ??
		               content.Logo;
		content.Backdrop = orderedResults.CombineLists(x => x.Images?["backdrop"] ?? []).FirstOrDefault()?.Url ??
		                   content.Backdrop;
		content.Thumbnail = orderedResults.CombineLists(x => x.Images?["thumbnail"] ?? []).FirstOrDefault()?.Url ??
		                    content.Thumbnail;

		content.AgeRatings = orderedResults.CombineDicts(x => x.AgeRatings ?? [], content.AgeRatings);

		content.ReleasedAt =
			Utils.TryParseIsoString(orderedResults.FirstNonEmptyValue(x => x.ReleaseDate), out DateTimeOffset rDate)
				? rDate
				: content.ReleasedAt;
		content.FinishedStreamingAt =
			Utils.TryParseIsoString(orderedResults.FirstNonEmptyValue(x => x.FinishDate), out DateTimeOffset fDate)
				? fDate
				: content.FinishedStreamingAt;
		content.UpdatedAt = DateTimeOffset.UtcNow;
	}

	private static void SetExternalId(DatabaseContent content, string providerId, string? externalId)
	{
		DatabaseContentExternalId? existing = content.ExternalIds
			.FirstOrDefault(x => x.ProviderId == providerId);

		if (string.IsNullOrWhiteSpace(externalId))
		{
			if (existing != null)
				content.ExternalIds.Remove(existing);
		}
		else if (existing == null)
		{
			content.ExternalIds.Add(new DatabaseContentExternalId
			{
				ContentId = content.Id,
				ProviderId = providerId,
				ExternalId = externalId
			});
		}
		else
		{
			existing.ExternalId = externalId;
		}
	}

	// CallPlugins(providerIds, "metadata.getMovie")
	// CallPlugins(providerIds, "metadata.getTv")
	// CallPlugins(providerIds, "metadata.getTvSeason", 1)
	// CallPlugins(providerIds, "metadata.getTvEpisode", 1, 1)
	private async Task<T[]> CallPlugins<T>(Dictionary<string, string> providerIds, string feature,
		params object[] extraArgs)
		where T : class
	{
		Dictionary<string, T> unorderedResults = [];
		foreach ((string providerId, string id) in providerIds)
		{
			IEnumerable<T> results =
				await pm.InvokeForPluginsWithPermission<T>($"metadata.provide.{providerId}", feature, id, extraArgs);
			T? first = results.FirstOrDefault();
			if (first == null) continue;
			unorderedResults[providerId] = first;
		}

		T[] orderedResults = unorderedResults.OrderBy(x =>
		{
			int index = config.Metadata.MetadataProviderOrder.IndexOf(x.Key);
			return index != -1 ? index : int.MaxValue;
		}).Select(x => x.Value).ToArray();

		return orderedResults.Length != 0
			? orderedResults
			: throw new Exception("None of the providers returned a result");
	}
}