using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Managers;

namespace OwnStream.Jobs;

[Job("FetchMetadata")]
public class FetchMetadataJob : IJob
{
	private DatabaseContext db = null!;
	private MetadataManager mm = null!;
	private TagManager tm = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		mm = serviceProvider.GetRequiredService<MetadataManager>();
		tm = serviceProvider.GetRequiredService<TagManager>();
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
			await db.SaveChangesAsync(cancellationToken);
			return (earlyContent, earlyEpisode);
		}

		DatabaseContent content = GetContent(args.ProviderIds, DatabaseContent.ContentType.Movie, args.LibraryId);
		await mm.RefreshMetadataOf(content, args.ProviderIds);
		tm.RecalculateSystemTagsFor(content);

		DatabaseEpisode? episode = null;
		if (args is { Episode: not null, Season: not null })
		{
			episode = await GetEpisode(content.Id, 1, 1, cancellationToken);
			episode.Title = content.Title;
			episode.Summary = "";
			episode.UpdatedAt = DateTimeOffset.UtcNow;
			episode.ReleasedAt = content.ReleasedAt;

			DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
			if (video != null)
			{
				video.EpisodeId = episode.Id;
				tm.RecalculateSystemTagsFor(video, true, content.Id);
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
			await db.SaveChangesAsync(cancellationToken);
			return (earlyContent, earlyEpisode);
		}

		DatabaseContent content = GetContent(args.ProviderIds, DatabaseContent.ContentType.Tv, args.LibraryId);
		await mm.RefreshMetadataOf(content, args.ProviderIds);
		tm.RecalculateSystemTagsFor(content);

		DatabaseEpisode? episode = null;
		if (args is { Season: not null })
		{
			DatabaseSeason season = await GetSeason(content.Id, args.Season.Value, cancellationToken);

			await mm.RefreshMetadataOf(season, args.ProviderIds);
		}
		if (args is { Episode: not null, Season: not null })
		{
			episode = await GetEpisode(content.Id, args.Season.Value, args.Episode.Value, cancellationToken);

			await mm.RefreshMetadataOf(episode, args.ProviderIds);

			DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
			if (video != null)
			{
				video.EpisodeId = episode.Id;
				tm.RecalculateSystemTagsFor(video, true, content.Id);
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
		return existing;
	}

	private async Task<DatabaseSeason> GetSeason(Guid parentContentId, int season, CancellationToken cancellationToken)
	{
		DatabaseSeason? existing = await db.Seasons.FirstOrDefaultAsync(
			x => x.ContentId == parentContentId && x.Index == season,
			cancellationToken: cancellationToken);
		if (existing != null) return existing;

		existing = new DatabaseSeason
		{
			ContentId = parentContentId,
			Index = season,
			Title = $"Season {season}",
			Description = "",
			Poster = null, 
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};
		db.Seasons.Add(existing);
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