using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/video/"), Authorize(AuthenticationSchemes = "ApiToken")]
public class VideoController(DatabaseContext db) : Controller
{
	[HttpGet("{id:guid}")]
	public Video? Get(Guid id)
	{
		DatabaseVideo? video = db.Videos.Include(x => x.Library)
			.FirstOrDefault(x => x.Id == id);

		if (video == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new Video(video)
		{
			Subtitles = GetSubtitles(video),
			PreviewFiles = GetPreviewFiles(video),
			Episode = new Episode(db.Episode.IgnoreAutoIncludes().FirstOrDefault(x => x.Id == video.EpisodeId))
		};
	}

	private SubtitleFile[]? GetSubtitles(DatabaseVideo video)
	{
		string subtitlesDir = Path.Join(video.Library.Path, video.Id.ToString(), "captions");
		if (!Directory.Exists(subtitlesDir)) return null;
		return Directory.GetFiles(subtitlesDir)
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
	}

	private PreviewFile[]? GetPreviewFiles(DatabaseVideo video)
	{
		string subtitlesDir = Path.Join(video.Library.Path, video.Id.ToString(), "trickplay");
		if (!Directory.Exists(subtitlesDir)) return null;
		string[] files = Directory.GetFiles(subtitlesDir).Select(x => Path.GetFileName(x)).ToArray();
		string[] mediumFiles = files.Where(x => x.StartsWith("medium_")).ToArray();
		List<PreviewFile> r = [];
		foreach (string file in files)
		{
			Console.WriteLine(file);
		}
		if (files.Contains("small.png"))
		{
			r.Add(new PreviewFile
			{
				Template = "small.png",
				FrameCount = 1,
				Rows = 10,
				Columns = 10,
				Period = null
			});
		}

		if (mediumFiles.Length > 0)
		{
			r.Add(new PreviewFile
			{
				Template = "medium_%d.png",
				FrameCount = mediumFiles.Length,
				Rows = 5,
				Columns = 5,
				Period = 5
			});
		}

		return r.ToArray();
	}
}