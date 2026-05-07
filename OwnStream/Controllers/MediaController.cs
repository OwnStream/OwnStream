using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

[EnableCors("Api")]
public class MediaController(DatabaseContext db) : Controller
{
	[Route("/Media/{id:guid}/{file}")]
	[Route("/Media/{id:guid}/{folder}/{file}")]
	[EnableCors("Api")]
	public IActionResult File(Guid id, string? folder, string file)
	{
		DatabaseVideo? video = db.Videos.Include(x => x.Library)
			.FirstOrDefault(x => x.Id == id);
		if (video == null) return NotFound();
		string path = folder == null
			? Path.Join(video.Library.Path, video.Id.ToString(), file)
			: Path.Join(video.Library.Path, video.Id.ToString(), folder, file);

		string mime = file.Split(".")[^1] switch
		{
			"m3u8" => "application/x-mpegURL",
			"m4s" => "video/iso.segment",
			"ts" => "video/mp2t",

			"png" => "image/png",
			"jpeg" => "image/jpeg",
			"jpg" => "image/jpeg",

			"srt" => "application/x-subrip",
			"vtt" => "text/vtt",
			"ass" => "text/x-ass",
			"ssa" => "text/x-ssa",
			
			_ => "application/octet-stream"
		};

		if (System.IO.File.Exists(path)) return PhysicalFile(path, mime);
		return NotFound();
	}

	[Route("/Media/{id:guid}/subtitles.json")]
	[EnableCors("Api")]
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
	[EnableCors("Api")]
	public IActionResult Segments(Guid id)
	{
		DatabaseVideo? video = db.Videos.Find(id);
		if (video == null) return NotFound();

		return Json(db.VideoSegments
			.Where(x => x.VideoId == id)
			.ToArray()
			.OrderBy(x => x.StartMilliseconds)
			.Select(x => new VideoSegment(x)));
	}	
}