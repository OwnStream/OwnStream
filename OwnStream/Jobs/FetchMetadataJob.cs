using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;

namespace OwnStream.Jobs;

public class FetchMetadataJob : IJob
{
	private DatabaseContext db = null!;
	private static bool providersReady = false;
	private static TMDbClient tmdb = new("d6c3b6af61d00e36435b1912b160082f");

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		await PrepProviders(cancellationToken);
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		switch (args.Type)
		{
			case "movie": await FetchMovieMetadata(job, args, cancellationToken); break;
			case "tv": await FetchShowMetadata(job, args, cancellationToken); break;
			default: throw new IndexOutOfRangeException($"Unexpected metadata type '{args.Type}'");
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task PrepProviders(CancellationToken cancellationToken)
	{
		await tmdb.GetConfigAsync();
	}

	private async Task FetchMovieMetadata(DatabaseFfmpegJob job, Arguments args, CancellationToken cancellationToken)
	{
		Movie? tmdbMovie = null;
		if (args.ProviderIds.TryGetValue("tmdb", out string? sTmdbId) && int.TryParse(sTmdbId, out int tmdbId))
			tmdbMovie = await tmdb.GetMovieAsync(tmdbId, MovieMethods.Translations | MovieMethods.Images,
				cancellationToken);

		DatabaseContent content = await GetContent(args.ProviderIds, DatabaseContent.ContentType.Movie, args.LibraryId,
			cancellationToken);

		if (args.ProviderIds.TryGetValue("imdb", out string? pImdbId)) content.ImdbId = pImdbId;
		if (args.ProviderIds.TryGetValue("tmdb", out string? pTmdbId) && int.TryParse(pTmdbId, out int iTmdbId))
			content.TmdbId = iTmdbId;

		content.Title = tmdbMovie?.OriginalTitle ?? tmdbMovie?.Title ?? "Missing title";
		content.Tagline = tmdbMovie?.Tagline ?? "";
		content.Description = tmdbMovie?.Overview ?? "";
		foreach (Translation translation in tmdbMovie?.Translations?.Translations ?? [])
		{
			if (translation.Iso_639_1 == null || translation.Iso_3166_1 == null || translation.Data == null) continue;
			string key = translation.Iso_639_1 + "_" + translation.Iso_3166_1;
			if (translation.Data.Name != null && translation.Data.Name.Trim().Length > 0)
				content.TranslatedTitle[key] = translation.Data.Name.Trim();
			if (translation.Data.Tagline != null && translation.Data.Tagline.Trim().Length > 0)
				content.TranslatedTagline[key] = translation.Data.Tagline.Trim();
			if (translation.Data.Overview != null && translation.Data.Overview.Trim().Length > 0)
				content.TranslatedDescription[key] = translation.Data.Overview.Trim();
		}

		if (tmdbMovie?.PosterPath != null)
			content.Poster = tmdb.GetImageUrl("original", tmdbMovie.PosterPath, true).ToString();
		if (tmdbMovie?.BackdropPath != null)
			content.Backdrop = tmdb.GetImageUrl("original", tmdbMovie.BackdropPath, true).ToString();
		if (tmdbMovie?.Images?.Logos?.Count > 0)
		{
			ImageData? logo = tmdbMovie?.Images?.Logos?.MaxBy(x => x.VoteAverage);
			if (logo?.FilePath != null)
				content.Logo = tmdb.GetImageUrl("original", logo.FilePath, true).ToString();
		}

		content.ReleasedAt = new DateTimeOffset(tmdbMovie?.ReleaseDate ?? DateTime.UnixEpoch).ToUniversalTime();
		content.UpdatedAt = DateTimeOffset.UtcNow;

		if (args is { Episode: not null, Season: not null })
		{
			DatabaseEpisode episode = await GetEpisode(content.Id, 1, 1, cancellationToken);
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
	}

	private async Task FetchShowMetadata(DatabaseFfmpegJob job, Arguments args, CancellationToken cancellationToken)
	{
		TvShow? tmdbShow = null;
		if (args.ProviderIds.TryGetValue("tmdb", out string? sTmdbId) && int.TryParse(sTmdbId, out int tmdbId))
			tmdbShow = await tmdb.GetTvShowAsync(tmdbId,
				TvShowMethods.Translations | TvShowMethods.Images | TvShowMethods.ContentRatings,
				cancellationToken: cancellationToken);

		DatabaseContent content = await GetContent(args.ProviderIds, DatabaseContent.ContentType.Tv, args.LibraryId, cancellationToken);
		if (args.ProviderIds.TryGetValue("imdb", out string? pImdbId)) content.ImdbId = pImdbId;
		if (args.ProviderIds.TryGetValue("tmdb", out string? pTmdbId) && int.TryParse(pTmdbId, out int iTmdbId))
			content.TmdbId = iTmdbId;
		if (args.ProviderIds.TryGetValue("tvdb", out string? pTvdbId) && int.TryParse(pTvdbId, out int iTvdbId))
			content.TvdbId = iTvdbId;
		if (args.ProviderIds.TryGetValue("tvMaze", out string? pTvMazeId) && int.TryParse(pTvMazeId, out int iTvMazeId))
			content.TvMazeId = iTvMazeId;

		content.Title = tmdbShow?.OriginalName ?? tmdbShow?.Name ?? "Missing title";
		content.Tagline = tmdbShow?.Tagline ?? "";
		content.Description = tmdbShow?.Overview ?? "";

		foreach (Translation translation in tmdbShow?.Translations?.Translations ?? [])
		{
			if (translation.Iso_639_1 == null || translation.Iso_3166_1 == null || translation.Data == null) continue;
			string key = translation.Iso_639_1 + "_" + translation.Iso_3166_1;
			if (translation.Data.Name != null && translation.Data.Name.Trim().Length > 0)
				content.TranslatedTitle[key] = translation.Data.Name.Trim();
			if (translation.Data.Tagline != null && translation.Data.Tagline.Trim().Length > 0)
				content.TranslatedTagline[key] = translation.Data.Tagline.Trim();
			if (translation.Data.Overview != null && translation.Data.Overview.Trim().Length > 0)
				content.TranslatedDescription[key] = translation.Data.Overview.Trim();
		}

		if (tmdbShow?.PosterPath != null)
			content.Poster = tmdb.GetImageUrl("original", tmdbShow.PosterPath, true).ToString();
		if (tmdbShow?.BackdropPath != null)
			content.Backdrop = tmdb.GetImageUrl("original", tmdbShow.BackdropPath, true).ToString();
		if (tmdbShow?.Images?.Logos?.Count > 0)
		{
			ImageData? logo = tmdbShow?.Images?.Logos?.MaxBy(x => x.VoteAverage);
			if (logo?.FilePath != null)
				content.Logo = tmdb.GetImageUrl("original", logo.FilePath, true).ToString();
		}

		content.ReleasedAt = new DateTimeOffset(tmdbShow?.FirstAirDate ?? DateTime.UnixEpoch).ToUniversalTime();
		content.FinishedStreamingAt = tmdbShow?.LastAirDate != null
			? new DateTimeOffset(tmdbShow.LastAirDate.Value).ToUniversalTime() : null;
		content.UpdatedAt = DateTimeOffset.UtcNow;
		foreach (ContentRating rating in tmdbShow?.ContentRatings?.Results ?? [])
		{
			if (rating.Iso_3166_1 == null || rating.Rating == null) continue;
			content.AgeRatings[rating.Iso_3166_1] = rating.Rating;
		}

		if (args is { Episode: not null, Season: not null })
		{
			DatabaseEpisode episode = await GetEpisode(content.Id, args.Season.Value, args.Episode.Value, cancellationToken);
			
			TvEpisode? tmdbEpisode = null;
			if (args.ProviderIds.TryGetValue("tmdb", out string? sTmdbEId) && int.TryParse(sTmdbEId, out int tmdbIdE))
				tmdbEpisode = await tmdb.GetTvEpisodeAsync(tmdbIdE, args.Season.Value, args.Episode.Value,
					TvEpisodeMethods.Translations | TvEpisodeMethods.Images,
					cancellationToken: cancellationToken);
			
			episode.Title = tmdbEpisode?.Name ?? $"Episode {args.Episode}";
			episode.Summary = tmdbEpisode?.Overview ?? "";
			episode.UpdatedAt = DateTimeOffset.UtcNow;
			episode.ReleasedAt = tmdbEpisode?.AirDate != null
				? new DateTimeOffset(tmdbEpisode.AirDate.Value).ToUniversalTime()
				: DateTimeOffset.UnixEpoch;
			if (tmdbEpisode?.StillPath != null)
				episode.Thumbnail = tmdb.GetImageUrl("original", tmdbEpisode.StillPath, true).ToString();

			foreach (Translation translation in tmdbEpisode?.Translations?.Translations ?? [])
			{
				if (translation.Iso_639_1 == null || translation.Iso_3166_1 == null || translation.Data == null) continue;
				string key = translation.Iso_639_1 + "_" + translation.Iso_3166_1;
				if (translation.Data.Name != null && translation.Data.Name.Trim().Length > 0)
					episode.TranslatedTitle[key] = translation.Data.Name.Trim();
				if (translation.Data.Overview != null && translation.Data.Overview.Trim().Length > 0)
					episode.TranslatedSummary[key] = translation.Data.Overview.Trim();
			}

			if (args.VideoId != null)
			{
				DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
				video?.EpisodeId = episode.Id;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task<DatabaseContent> GetContent(Dictionary<string, string> providerIds,
		DatabaseContent.ContentType type, Guid libraryId,
		CancellationToken cancellationToken)
	{
		int? tmdbId = int.TryParse(providerIds["tmdb"], out int r) ? r : null;
		DatabaseContent? existing = await db.Content.FirstOrDefaultAsync(
			x => x.TmdbId == tmdbId && x.LibraryId == libraryId && x.Type == type,
			cancellationToken: cancellationToken);
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