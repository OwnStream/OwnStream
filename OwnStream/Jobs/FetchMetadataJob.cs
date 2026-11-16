using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Plugin;
using OwnStream.PluginCore.ResultTypes;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;

namespace OwnStream.Jobs;

[Job("FetchMetadata")]
public class FetchMetadataJob : IJob
{
	private DatabaseContext db = null!;
	private PluginManager pm = null!;
	private Configuration config = null!;
	private static bool providersReady = false;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		pm = serviceProvider.GetRequiredService<PluginManager>();
		config = serviceProvider.GetRequiredService<Configuration>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		(DatabaseContent Content, DatabaseEpisode? Episode) updatedItems = args.Type switch
		{
			"movie" => await FetchMovieMetadata(job, args, cancellationToken),
			"tv" => await FetchShowMetadata(job, args, cancellationToken),
			_ => throw new IndexOutOfRangeException($"Unexpected metadata type '{args.Type}'")
		};

		job.Message = updatedItems.Content.Type switch
		{
			DatabaseContent.ContentType.Movie => $"Got metadata for movie '{updatedItems.Content.Title}'",
			DatabaseContent.ContentType.Tv =>
				$"Got metadata for '{updatedItems.Content.Title}' S{updatedItems.Episode?.Season} E{updatedItems.Episode?.Episode}: {updatedItems.Episode?.Title}",
			_ => job.Message
		};

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task<(DatabaseContent content, DatabaseEpisode? episode)> FetchMovieMetadata(DatabaseFfmpegJob job,
		Arguments args, CancellationToken cancellationToken)
	{
		if (args.ProviderIds.Count == 0)
		{
			DatabaseContent earlyContent = GetContent(args.ProviderIds, DatabaseContent.ContentType.Movie,
				args.LibraryId);
			DatabaseEpisode? earlyEpisode = args is { Episode: not null, Season: not null }
				? await GetEpisode(earlyContent.Id, args.Season.Value, args.Episode.Value, cancellationToken)
				: null;
			return (earlyContent, earlyEpisode);
		}

		Dictionary<string, MetadataMovieResult> unorderedResults = [];
		foreach ((string providerId, string id) in args.ProviderIds)
		{
			IEnumerable<MetadataMovieResult> results =
				await pm.InvokeForPluginsWithPermission<MetadataMovieResult>($"metadata.provide.{providerId}",
					"metadata.getMovie", id);
			MetadataMovieResult? first = results.FirstOrDefault();
			if (first == null) continue;
			unorderedResults[providerId] = first;
		}

		MetadataMovieResult[] orderedResults = unorderedResults.OrderBy(x =>
		{
			int index = config.Metadata.MetadataProviderOrder.IndexOf(x.Key);
			return index != -1 ? index : int.MaxValue;
		}).Select(x => x.Value).ToArray();

		if (orderedResults.Length == 0)
			throw new Exception("None of the providers returned a result");

		DatabaseContent content = GetContent(args.ProviderIds, DatabaseContent.ContentType.Movie, args.LibraryId);

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

		DatabaseEpisode? episode = null;
		if (args is { Episode: not null, Season: not null })
		{
			episode = await GetEpisode(content.Id, 1, 1, cancellationToken);
			episode.Title = "movie";
			episode.Summary = "movie";
			episode.UpdatedAt = DateTimeOffset.UtcNow;
			episode.ReleasedAt = content.ReleasedAt;

			if (args.VideoId != null)
			{
				DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
				video?.EpisodeId = episode.Id;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		return (content, episode);
	}

	private async Task<(DatabaseContent content, DatabaseEpisode? episode)> FetchShowMetadata(DatabaseFfmpegJob job,
		Arguments args, CancellationToken cancellationToken)
	{
		if (args.ProviderIds.Count == 0)
		{
			DatabaseContent earlyContent = GetContent(args.ProviderIds, DatabaseContent.ContentType.Tv,
				args.LibraryId);
			DatabaseEpisode? earlyEpisode = args is { Episode: not null, Season: not null }
				? await GetEpisode(earlyContent.Id, args.Season.Value, args.Episode.Value, cancellationToken)
				: null;
			return (earlyContent, earlyEpisode);
		}

		Dictionary<string, MetadataTvResult> unorderedResults = [];
		foreach ((string providerId, string id) in args.ProviderIds)
		{
			IEnumerable<MetadataTvResult> results =
				await pm.InvokeForPluginsWithPermission<MetadataTvResult>($"metadata.provide.{providerId}",
					"metadata.getTv", id);
			MetadataTvResult? first = results.FirstOrDefault();
			if (first == null) continue;
			unorderedResults[providerId] = first;
		}

		MetadataTvResult[] orderedResults = unorderedResults.OrderBy(x =>
		{
			int index = config.Metadata.MetadataProviderOrder.IndexOf(x.Key);
			return index != -1 ? index : int.MaxValue;
		}).Select(x => x.Value).ToArray();

		if (orderedResults.Length == 0)
			throw new Exception("None of the providers returned a result");

		DatabaseContent content = GetContent(args.ProviderIds, DatabaseContent.ContentType.Tv, args.LibraryId);

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

		DatabaseEpisode? episode = null;
		if (args is { Episode: not null, Season: not null })
		{
			episode = await GetEpisode(content.Id, args.Season.Value, args.Episode.Value, cancellationToken);

			Dictionary<string, MetadataEpisodeResult> unorderedEpisodeResults = [];
			foreach ((string providerId, string id) in args.ProviderIds)
			{
				IEnumerable<MetadataEpisodeResult> results =
					await pm.InvokeForPluginsWithPermission<MetadataEpisodeResult>($"metadata.provide.{providerId}",
						"metadata.getTvEpisode", id, args.Season, args.Episode);
				MetadataEpisodeResult? first = results.FirstOrDefault();
				if (first == null) continue;
				unorderedEpisodeResults[providerId] = first;
			}

			MetadataEpisodeResult[] orderedEpisodeResults = unorderedEpisodeResults.OrderBy(x =>
			{
				int index = config.Metadata.MetadataProviderOrder.IndexOf(x.Key);
				return index != -1 ? index : int.MaxValue;
			}).Select(x => x.Value).ToArray();

			if (orderedEpisodeResults.Length == 0)
				throw new Exception("None of the providers returned a result");

			episode.Title = orderedEpisodeResults.FirstNonEmptyValue(x => x.Title) ?? episode.Title;
			episode.Summary = orderedEpisodeResults.FirstNonEmptyValue(x => x.Summary) ?? episode.Summary;

			episode.TranslatedTitle =
				orderedEpisodeResults.CombineDicts(x => x.TitleTranslations ?? [], episode.TranslatedTitle);
			episode.TranslatedSummary =
				orderedEpisodeResults.CombineDicts(x => x.SummaryTranslations ?? [], episode.TranslatedSummary);

			episode.Thumbnail = orderedEpisodeResults.CombineLists(x => x.Thumbnails ?? []).FirstOrDefault()?.Url ??
			                    episode.Thumbnail;

			episode.UpdatedAt = DateTimeOffset.UtcNow;
			episode.ReleasedAt = Utils.TryParseIsoString(orderedEpisodeResults.FirstNonEmptyValue(x => x.ReleaseDate),
				out DateTimeOffset erDate)
				? erDate
				: episode.ReleasedAt;

			if (args.VideoId != null)
			{
				DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
				video?.EpisodeId = episode.Id;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		return (content, episode);
	}

	private DatabaseContent GetContent(Dictionary<string, string> providerIds,
		DatabaseContent.ContentType type, Guid libraryId)
	{
		DatabaseContent? existing = db.Content
			.Include(x => x.ExternalIds)
			.Where(x =>
				x.LibraryId == libraryId &&
				x.Type == type)
			.ToArray()
			.FirstOrDefault(x =>
				x.ExternalIds.Any(c => providerIds.GetValueOrDefault(c.ProviderId, "") == c.ExternalId)
			);
		if (existing != null) return existing;

		existing = new DatabaseContent
		{
			Id = Guid.NewGuid(),
			LibraryId = libraryId,
			Type = type,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};
		db.Content.Add(existing);
		foreach ((string providerId, string externalId) in providerIds)
			SetExternalId(existing, providerId, externalId);
		return existing;
	}

	private async Task<DatabaseEpisode> GetEpisode(Guid parentContentId, int season, int episode,
		CancellationToken cancellationToken)
	{
		DatabaseEpisode? existing = await db.Episode.FirstOrDefaultAsync(
			x => x.ParentContentId == parentContentId && x.Season == season && x.Episode == episode,
			cancellationToken: cancellationToken);
		if (existing != null) return existing;

		existing = new DatabaseEpisode
		{
			Id = Guid.NewGuid(),
			ParentContentId = parentContentId,
			Season = season,
			Episode = episode,
			CreatedAt = DateTimeOffset.UtcNow,
			ReleasedAt = DateTimeOffset.UtcNow
		};
		db.Episode.Add(existing);
		return existing;
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

	public class Arguments
	{
		public Guid? VideoId { get; set; }
		public Guid LibraryId { get; set; }
		public string Type { get; set; }
		public int? Season { get; set; }
		public int? Episode { get; set; }
		public Dictionary<string, string> ProviderIds { get; set; }
	}
}