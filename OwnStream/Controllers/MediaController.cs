using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;

namespace OwnStream.Controllers;

public class MediaController(DatabaseContext db) : Controller
{
	[Route("/Media/{id:guid}/{file}")]
	[Route("/Media/{id:guid}/{folder}/{file}")]
	public IActionResult File(Guid id, string? folder, string file)
	{
		DatabaseVideo? video = db.Videos.Include(x => x.Library)
			.FirstOrDefault(x => x.Id == id);
		if (video == null) return NotFound();
		string path = folder == null
			? Path.Join(video.Library.Path, video.Id.ToString(), file)
			: Path.Join(video.Library.Path, video.Id.ToString(), folder, file);

		string mime = file.Split(".")[1] switch
		{
			"m3u8" => "application/x-mpegURL",
			_ => "application/octet-stream"
		};

		if (System.IO.File.Exists(path)) return PhysicalFile(path, mime);
		return NotFound();
	}

	[Route("/Media/{id:guid}/subtitles.json")]
	public IActionResult Subtitles(Guid id)
	{
		DatabaseVideo? video = db.Videos.Include(x => x.Library)
			.FirstOrDefault(x => x.Id == id);
		if (video == null) return NotFound();
		string subtitlesDir = Path.Join(video.Library.Path, video.Id.ToString(), "captions");
		if (!Directory.Exists(subtitlesDir)) return NotFound();
		SubtitleFile[] subtitles = Directory.GetFiles(subtitlesDir)
			.Select(x => x.Split("/").Last())
			.GroupBy(x => string.Join("", x.Split('.').SkipLast(1)))
			.Select(x =>
			{
				// eng.English.default.3.vtt
				List<string> parts = x.First().Split(".").ToList();
				string lang = parts[0];
				string title = parts[1];
				parts = parts.Skip(2).ToList();
				return new SubtitleFile
				{
					Id = int.Parse(parts[^2]),
					Files = x.ToDictionary(e => e.Split('.')[^1], e => e),
					Default = parts.Contains("default"),
					Forced = parts.Contains("forced"),
					Language = lang,
					Title = title
				};
			}).ToArray();
		return Json(subtitles);
	}

	[Route("/Media/{id:guid}/segments.json")]
	public IActionResult Segments(Guid id)
	{
		IIncludableQueryable<DatabaseVideo, DatabaseLibrary> q = db.Videos
			.Include(x => x.Library)
			.Include(x => x.Episode)
			.ThenInclude(x => x!.ParentContent)
			.ThenInclude(x => x.Episodes)
			.ThenInclude(x => x.Videos)
			.Include(x => x.Episode)
			.ThenInclude(x => x!.ParentContent)
			.ThenInclude(x => x.Library);
		DatabaseVideo? thisVideo = q.FirstOrDefault(x => x.Id == id);
		using FileStream? fs1 = thisVideo != null
			? System.IO.File.OpenRead(Path.Join(thisVideo.Library.Path, thisVideo.Id.ToString(), "fingerprints.fp"))
			: null;
		DetectIntroSectionsJob.FingerprintFile? thisFile =
			fs1 != null ? DetectIntroSectionsJob.FingerprintFile.ReadFromStream(fs1) : null;
		if (thisFile == null) return NotFound("fp1 not found");


		DetectIntroSectionsJob.OtherEpisode[] otherEpisodes = thisVideo?.Episode?.ParentContent.Episodes
			.Where(x => x.Season == thisVideo.Episode?.Season)
			.Where(x => x.Id != thisVideo.EpisodeId)
			.Select(x =>
			{
				DatabaseVideo? video = x.Videos.FirstOrDefault();
				return video != null
					? new DetectIntroSectionsJob.OtherEpisode
					{
						VideoId = video.Id,
						EpisodeId = x.Id,
						LibraryId = x.ParentContent.LibraryId,
						SeasonNum = x.Season,
						EpisodeNum = x.Episode,
						Path = Path.Join(
							x.ParentContent.Library.Path,
							video.Id.ToString())
					}
					: null;
			})
			.Where(x => x != null)
			.Cast<DetectIntroSectionsJob.OtherEpisode>()
			.ToArray() ?? [];
		List<DetectIntroSectionsJob.SimilarRange> allRanges = [];

		foreach (DetectIntroSectionsJob.OtherEpisode otherEpisode in otherEpisodes)
		{
			otherEpisode.LoadFile();
			if (otherEpisode.File == null) continue;
			byte[,] similarity = DetectIntroSectionsJob.CompareFiles(thisFile, otherEpisode.File);
			DetectIntroSectionsJob.SimilarRange[] ranges = DetectIntroSectionsJob.GetSimilarRanges(similarity, 200, 30);
			allRanges.AddRange(ranges);
		}
		
		allRanges = DetectIntroSectionsJob.MergeRanges(allRanges);

		return Json(allRanges);
	}
}