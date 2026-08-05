using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Services;

namespace OwnStream.Jobs;

[Job("ScanLibrary")]
public partial class ScanLibraryJob : IJob
{
	private DatabaseContext db = null!;
	private IFfmpegJobQueueService queueService = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		queueService = serviceProvider.GetRequiredService<IFfmpegJobQueueService>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		DatabaseInputLibrary? inputLibrary = await db.InputLibraries
			.Include(x => x.TranscodeLibrary)
			.FirstOrDefaultAsync(x => x.Id == args.InputLibraryId, cancellationToken);
		DatabaseLibrary? transcodeLibrary = inputLibrary?.TranscodeLibrary;

		if (inputLibrary == null) throw new Exception("Invalid input library ID");
		if (transcodeLibrary == null) throw new Exception("Invalid transcode library ID");

		switch (inputLibrary.Type)
		{
			case DatabaseInputLibrary.InputLibraryType.Tv:
				await ScanShowLibraryAndCreateItems(db, job, queueService, inputLibrary, transcodeLibrary);
				break;
			case DatabaseInputLibrary.InputLibraryType.Movie:
				await ScanMovieLibraryAndCreateItems(db, job, queueService, inputLibrary, transcodeLibrary);
				break;
			default:
				throw new IndexOutOfRangeException("Unexpected value for input library type");
		}
	}

	private static async Task ScanMovieLibraryAndCreateItems(DatabaseContext db, DatabaseFfmpegJob job,
		IFfmpegJobQueueService queueService, DatabaseInputLibrary inputLibrary, DatabaseLibrary transcodeLibrary)
	{
		job.Message = "Scanning Movie library...";
		job.Status = DatabaseFfmpegJob.JobStatus.Processing;
		await db.SaveChangesAsync();

		List<ParsedMovie> newMovies = ScanMovieLibrary(inputLibrary.Path, db);

		if (newMovies.Count == 0)
		{
			job.Message = "No new items found to import";
			return;
		}

		job.ProgressMax = newMovies.Count;
		job.Progress = 0;
		job.Message = $"Importing {newMovies.Count} new movies";
		await db.SaveChangesAsync();

		foreach (ParsedMovie movie in newMovies)
		{
			DatabaseContent content = new()
			{
				Id = Guid.NewGuid(),
				LibraryId = transcodeLibrary.Id,
				Type = DatabaseContent.ContentType.Movie,
				Title = movie.Name,
				TranslatedTitle = [],
				Tagline = "",
				TranslatedTagline = [],
				Description = "",
				TranslatedDescription = [],
				Poster = null,
				Banner = null,
				Logo = null,
				Backdrop = null,
				Thumbnail = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow,
				ReleasedAt = movie.Year == null
					? DateTimeOffset.UnixEpoch
					: new DateTimeOffset(movie.Year.Value, 1, 1, 0, 0, 0, TimeSpan.Zero),
				FinishedStreamingAt = null,
				AgeRatings = []
			};
			DatabaseEpisode episode = new()
			{
				Id = Guid.NewGuid(),
				ParentContentId = content.Id,
				Season = 1,
				Episode = 1,
				Title = "movie",
				TranslatedTitle = [],
				Summary = "movie",
				TranslatedSummary = [],
				Thumbnail = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow,
				ReleasedAt = DateTimeOffset.UtcNow
			};
			db.Content.Add(content);
			db.Episode.Add(episode);

			foreach ((string provider, string id) in movie.ProviderIds)
			{
				db.ContentExternalIds.Add(new DatabaseContentExternalId
				{
					ContentId = content.Id,
					ProviderId = provider,
					ExternalId = id
				});
			}

			Guid videoId = Guid.NewGuid();
			await queueService.EnqueueAsync("TranscodeFull", movie.Filename,
				Path.Join(transcodeLibrary.Path, videoId.ToString()), new TranscodeJob.Arguments
				{
					DeleteAfterTranscode = false,
					Metadata = null,
					VideoId = videoId,
					LibraryId = transcodeLibrary.Id
				});
			await queueService.EnqueueAsync("FetchMetadata", "", "", new FetchMetadataJob.Arguments
			{
				VideoId = videoId,
				LibraryId = transcodeLibrary.Id,
				Type = "movie",
				Season = 1,
				Episode = 1,
				ProviderIds = movie.ProviderIds
			});

			job.Progress++;
			await db.SaveChangesAsync();
		}

		job.Message = $"Added {newMovies.Count} new movies";
	}

	private static async Task ScanShowLibraryAndCreateItems(DatabaseContext db, DatabaseFfmpegJob job,
		IFfmpegJobQueueService queueService, DatabaseInputLibrary inputLibrary, DatabaseLibrary transcodeLibrary)
	{
		job.Message = "Scanning TV library...";
		job.Status = DatabaseFfmpegJob.JobStatus.Processing;
		await db.SaveChangesAsync();

		(List<ParsedShow> newShows, List<ParsedEpisode> newEpisodes) = ScanShowLibrary(inputLibrary.Path, db);

		if (newShows.Count == 0 && newEpisodes.Count == 0)
		{
			job.Message = "No new items found to import";
			return;
		}

		job.ProgressMax = newShows.Count + newShows.Sum(x => x.Episodes.Length) + newEpisodes.Count;
		job.Progress = 0;
		job.Message =
			$"Importing {newShows.Count} new shows and {newShows.Sum(x => x.Episodes.Length) + newEpisodes.Count} new episodes";
		await db.SaveChangesAsync();

		foreach (ParsedShow show in newShows)
		{
			DatabaseContent content = new()
			{
				Id = Guid.NewGuid(),
				LibraryId = transcodeLibrary.Id,
				Type = DatabaseContent.ContentType.Tv,
				Title = show.Name,
				TranslatedTitle = [],
				Tagline = "",
				TranslatedTagline = [],
				Description = "",
				TranslatedDescription = [],
				Poster = null,
				Banner = null,
				Logo = null,
				Backdrop = null,
				Thumbnail = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow,
				ReleasedAt = DateTimeOffset.UnixEpoch,
				FinishedStreamingAt = null,
				AgeRatings = []
			};
			db.Content.Add(content);

			foreach ((string provider, string id) in show.ProviderIds)
			{
				db.ContentExternalIds.Add(new DatabaseContentExternalId
				{
					ContentId = content.Id,
					ProviderId = provider,
					ExternalId = id
				});
			}

			job.Progress++;
			if (job.Progress % 5 == 0)
				await db.SaveChangesAsync();

			foreach (ParsedEpisode episode in show.Episodes.OrderBy(x => x.Season).ThenBy(x => x.Episode))
			{
				if (episode.Season == null || episode.Episode == null)
				{
					job.Progress++;
					if (job.Progress % 5 == 0)
						await db.SaveChangesAsync();

					continue;
				}

				DatabaseEpisode dbEpisode = new()
				{
					Id = Guid.NewGuid(),
					ParentContentId = content.Id,
					Season = episode.Season!.Value,
					Episode = episode.Episode!.Value,
					Title = Path.GetFileNameWithoutExtension(episode.Filename),
					TranslatedTitle = [],
					Summary = "",
					TranslatedSummary = [],
					Thumbnail = null,
					CreatedAt = DateTimeOffset.UtcNow,
					UpdatedAt = DateTimeOffset.UtcNow,
					ReleasedAt = DateTimeOffset.UnixEpoch
				};
				db.Episode.Add(dbEpisode);
				Guid videoId = Guid.NewGuid();
				await queueService.EnqueueAsync("TranscodeFull", episode.Filename,
					Path.Join(transcodeLibrary.Path, videoId.ToString()), new TranscodeJob.Arguments
					{
						DeleteAfterTranscode = false,
						Metadata = null,
						VideoId = videoId,
						LibraryId = transcodeLibrary.Id
					});
				await queueService.EnqueueAsync("FetchMetadata", "", "", new FetchMetadataJob.Arguments
				{
					VideoId = videoId,
					LibraryId = transcodeLibrary.Id,
					Type = "tv",
					Season = episode.Season,
					Episode = episode.Episode,
					ProviderIds = show.ProviderIds
				});
				job.Progress++;
				if (job.Progress % 5 == 0)
					await db.SaveChangesAsync();
			}
		}

		Dictionary<Guid, Dictionary<string, string>> databaseContentExternalIdsMap = newEpisodes
			.GroupBy(x => x.ContentId)
			.Where(x => x.Key != null)
			.Select(x => x.Key!.Value)
			.ToDictionary(x => x,
				id => db.ContentExternalIds.Where(x => x.ContentId == id)
					.ToDictionary(e => e.ProviderId, e => e.ExternalId));

		foreach (ParsedEpisode episode in newEpisodes.OrderBy(x => x.Season).ThenBy(x => x.Episode))
		{
			if (episode.Season == null || episode.Episode == null || episode.ContentId == null)
			{
				job.Progress++;
				if (job.Progress % 5 == 0)
					await db.SaveChangesAsync();

				continue;
			}

			DatabaseEpisode dbEpisode = new()
			{
				Id = Guid.NewGuid(),
				ParentContentId = episode.ContentId!.Value,
				Season = episode.Season!.Value,
				Episode = episode.Episode!.Value,
				Title = Path.GetFileNameWithoutExtension(episode.Filename),
				TranslatedTitle = [],
				Summary = "",
				TranslatedSummary = [],
				Thumbnail = null,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow,
				ReleasedAt = DateTimeOffset.UnixEpoch
			};
			db.Episode.Add(dbEpisode);
			Guid videoId = Guid.NewGuid();
			await queueService.EnqueueAsync("TranscodeFull", episode.Filename,
				Path.Join(transcodeLibrary.Path, videoId.ToString()), new TranscodeJob.Arguments
				{
					DeleteAfterTranscode = false,
					Metadata = null,
					VideoId = videoId,
					LibraryId = transcodeLibrary.Id
				});
			await queueService.EnqueueAsync("FetchMetadata", "", "", new FetchMetadataJob.Arguments
			{
				VideoId = videoId,
				LibraryId = transcodeLibrary.Id,
				Type = "tv",
				Season = episode.Season,
				Episode = episode.Episode,
				ProviderIds = databaseContentExternalIdsMap[episode.ContentId!.Value]
			});
			job.Progress++;
			if (job.Progress % 5 == 0)
				await db.SaveChangesAsync();
		}

		job.Message =
			$"Added {newShows.Count} new shows and {newShows.Sum(x => x.Episodes.Length) + newEpisodes.Count} new episodes";
	}

	private static List<ParsedMovie> ScanMovieLibrary(string path, DatabaseContext db)
	{
		List<ParsedMovie> newMovies = [];

		DirectoryInfo dir = new(path);
		if (!dir.Exists)
			throw new Exception($"Directory at '{path}' does not exist");

		if (dir.GetFiles(".ignore").Length != 0)
			throw new Exception("Directory contains a .ignore file, it cannot be scanned.");

		List<ParsedMovie> allMovies = [];

		// Scan directories (Movie Title (2012) [externalid=tt1234]/)
		foreach (DirectoryInfo subDir in dir.GetDirectories())
		{
			ParsedMovie? movie = ScanMovieDirectory(subDir);
			if (movie != null) allMovies.Add(movie);
		}

		// Scan files (Movie Title (2012) [externalid=tt1234].mp4)
		foreach (FileInfo file in dir.GetFiles())
		{
			ParsedMovie? movie = ScanMovieFile(file);
			if (movie != null) allMovies.Add(movie);
		}

		foreach (ParsedMovie movie in allMovies)
		{
			DatabaseContent? content = db.Content.FirstOrDefault(x => x.Title == movie.Name);

			// If not matched by name, check by external provider IDs
			if (content == null && movie.ProviderIds.Count > 0)
			{
				DatabaseContentExternalId[] externalIds = movie.ProviderIds.Select(x =>
						db.ContentExternalIds.FirstOrDefault(e =>
#pragma warning disable CA1862 // Not supported by EFCore
							e.ProviderId.ToLower() == x.Key.ToLower() && e.ExternalId == x.Value))
#pragma warning restore CA1862
					.Where(x => x != null)
					.Cast<DatabaseContentExternalId>()
					.ToArray();
				if (externalIds.Length > 0)
				{
					Guid possibleId = externalIds[0].ContentId;
					if (externalIds.All(x => x.ContentId == possibleId))
						content = db.Content.FirstOrDefault(x => x.Id == possibleId);
				}
			}

			if (content == null)
			{
				newMovies.Add(movie);
			}
		}

		return newMovies;
	}

	private static (List<ParsedShow> shows, List<ParsedEpisode> episodes) ScanShowLibrary(string path,
		DatabaseContext db)
	{
		List<ParsedShow> newShows = [];
		List<ParsedEpisode> newEpisodes = [];

		DirectoryInfo dir = new(path);
		if (!dir.Exists)
			throw new Exception($"Directory at '{path}' does not exist");

		if (dir.GetFiles(".ignore").Length != 0)
			throw new Exception("Directory contains a .ignore file, it cannot be scanned.");

		List<ParsedShow> allShows = [];

		foreach (DirectoryInfo subDir in dir.GetDirectories())
		{
			ParsedShow? show = ScanSeriesRootDirectory(subDir);
			if (show != null) allShows.AddRange(show);
		}

		foreach (ParsedShow show in allShows)
		{
			DatabaseContent? content = db.Content.FirstOrDefault(x => x.Title == show.Name);

			// If not matched by name, check by external provider IDs
			// Possible if the original title != folder title, for example: 
			// Saved in database: "君の名は。" / Folder name: "Your Name."
			if (content == null && show.ProviderIds.Count > 0)
			{
				DatabaseContentExternalId[] externalIds = show.ProviderIds.Select(x =>
						db.ContentExternalIds.FirstOrDefault(e =>
#pragma warning disable CA1862 // Not supported by EFCore
							e.ProviderId.ToLower() == x.Key.ToLower() && e.ExternalId == x.Value))
#pragma warning restore CA1862
					.Where(x => x != null)
					.Cast<DatabaseContentExternalId>()
					.ToArray();
				if (externalIds.Length > 0)
				{
					Guid possibleId = externalIds[0].ContentId;
					if (externalIds.All(x => x.ContentId == possibleId))
						content = db.Content.FirstOrDefault(x => x.Id == possibleId);
				}
			}

			if (content == null)
			{
				newShows.Add(show);
				continue;
			}

			foreach (ParsedEpisode episode in show.Episodes.OrderBy(x => x.Season).ThenBy(x => x.Episode))
			{
				DatabaseEpisode? dbEpisode = db.Episode.FirstOrDefault(x =>
					x.ParentContentId == content!.Id && x.Season == episode.Season && x.Episode == episode.Episode);
				if (dbEpisode != null) continue;

				episode.ContentId = content.Id;
				newEpisodes.Add(episode);
			}
		}

		return (newShows, newEpisodes);
	}

	private static ParsedMovie? ScanMovieDirectory(DirectoryInfo dir)
	{
		if (dir.GetFiles(".ignore").Length != 0)
			return null;

		FileInfo[] videoFiles = dir.GetFiles();
		if (videoFiles.Length == 0)
			return null;

		ProviderMatchResponse dirMatch = MatchProvider(dir.Name);
		ProviderMatchResponse fileMatch = MatchProvider(videoFiles[0].FullName);

		Dictionary<string, string> combinedProviderIds = dirMatch.ProviderIds.ToDictionary(x => x.Key, x => x.Value);
		foreach ((string providerId, string externalId) in fileMatch.ProviderIds)
			combinedProviderIds[providerId] = externalId;

		return new ParsedMovie
		{
			Name = dirMatch.CleanName,
			ProviderIds = combinedProviderIds,
			Year = fileMatch.Year ?? dirMatch.Year,
			Filename = videoFiles[0].FullName
		};
	}

	private static ParsedMovie? ScanMovieFile(FileInfo file)
	{
		if (file.Name.StartsWith("."))
			return null;

		string nameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
		ProviderMatchResponse match = MatchProvider(nameWithoutExtension);

		return new ParsedMovie
		{
			Name = match.CleanName,
			ProviderIds = match.ProviderIds.ToDictionary(x => x.Key, x => x.Value),
			Year = match.Year,
			Filename = file.FullName
		};
	}

	private static ParsedShow? ScanSeriesRootDirectory(DirectoryInfo dir)
	{
		if (dir.GetFiles(".ignore").Length != 0)
			return null;

		DirectoryInfo[] seasonDirs = dir.GetDirectories();

		ProviderMatchResponse match = MatchProvider(dir.Name);
		List<ParsedEpisode> allEpisodes = [];
		foreach (DirectoryInfo subDir in seasonDirs.OrderBy(x => x.Name))
			allEpisodes.AddRange(ScanSeriesSeasonDirectory(subDir).Select(x =>
			{
				x.SeriesName = dir.Name;
				return x;
			}));

		return new ParsedShow
		{
			Name = match.CleanName,
			ProviderIds = match.ProviderIds.ToDictionary(x => x.Key, x => x.Value),
			Episodes = allEpisodes.ToArray()
		};
	}

	private static List<ParsedEpisode> ScanSeriesSeasonDirectory(DirectoryInfo dir)
	{
		int? season = ReadSeasonFileName(dir.Name);
		List<ParsedEpisode> episodes = new();

		foreach (FileInfo file in dir.GetFiles())
		{
			episodes.Add(new()
			{
				Season = season,
				Episode = ReadSeriesFileName(file.Name),
				Filename = file.FullName,
				SeriesName = "!"
			});
		}

		return episodes;
	}

	private static ProviderMatchResponse MatchProvider(string name)
	{
		Regex providerRegex = ProviderIdRegex();
		MatchCollection matches = providerRegex.Matches(name);

		Regex yearRegex = YearRegex();
		Match yearMatch = yearRegex.Match(name);
		int? year = null;

		if (matches.Count == 0 && !yearMatch.Success)
			return new ProviderMatchResponse
			{
				CleanName = name,
				Year = null,
				ProviderIds = []
			};

		string cleanName = name;
		foreach (Match m in matches)
		{
			cleanName = cleanName.Replace(m.Value, "").Trim();
		}

		if (yearMatch.Success)
		{
			year = int.Parse(yearMatch.Groups[1].Value);
			cleanName = cleanName.Replace(yearMatch.Value, "").Trim();
		}

		return new ProviderMatchResponse
		{
			CleanName = cleanName,
			Year = year,
			ProviderIds = matches
				.Where(x => !string.IsNullOrEmpty(x.Groups[1].Value) && x.Groups.Count > 1 &&
				            !string.IsNullOrEmpty(x.Groups[2].Value))
				.Select(x => new KeyValuePair<string, string>(x.Groups[1].Value, x.Groups[2].Value))
		};
	}

	private static int? ReadSeasonFileName(string name)
	{
		Regex seasonRegex = SeasonRegex();
		Match match = seasonRegex.Match(name);
		if (match.Success) return int.Parse(match.Groups[1].Value);

		Regex digitRegex = DigitRegex();
		Match digitMatch = digitRegex.Match(name);
		if (digitMatch.Success) return int.Parse(digitMatch.Groups[1].Value);

		return null;
	}

	private static int? ReadSeriesFileName(string name)
	{
		Regex episodeRegex = EpisodeRegex();
		Match match = episodeRegex.Match(name);
		if (!match.Success) return null;

		return int.Parse(match.Groups[1].Value);
	}

	private class ProviderMatchResponse
	{
		public string CleanName { get; set; }
		public int? Year { get; set; }
		public IEnumerable<KeyValuePair<string, string>> ProviderIds { get; set; }
	}

	private class ParsedShow
	{
		public string Name { get; set; }
		public Dictionary<string, string> ProviderIds { get; set; }
		public ParsedEpisode[] Episodes { get; set; }
	}

	private class ParsedEpisode
	{
		public int? Season { get; set; }
		public int? Episode { get; set; }
		public string SeriesName { get; set; }
		public Guid? ContentId { get; set; }
		public string Filename { get; set; }
	}

	private class ParsedMovie
	{
		public string Name { get; set; }
		public Dictionary<string, string> ProviderIds { get; set; }
		public string Filename { get; set; }
		public int? Year { get; set; }
	}

	[GeneratedRegex(@"\[(\S+?)(?:id)?[-=](\S+?)?\]")]
	private static partial Regex ProviderIdRegex();

	[GeneratedRegex(@"\((\d{4})\)")]
	private static partial Regex YearRegex();

	[GeneratedRegex(@"\b(\d+)\b")]
	private static partial Regex DigitRegex();

	[GeneratedRegex(@"S(\d+)")]
	private static partial Regex SeasonRegex();

	[GeneratedRegex(@"E(\d+)")]
	private static partial Regex EpisodeRegex();

	public class Arguments
	{
		public Guid InputLibraryId { get; set; }
	}
}