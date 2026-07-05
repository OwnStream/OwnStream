using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using System.Text;

namespace OwnStream.Controllers;

[EnableCors("Api")]
public class MediaController(DatabaseContext db) : Controller
{
	[Route("/Media/{id:guid}/{file}")]
	[Route("/Media/{id:guid}/{folder}/{file}")]
	[EnableCors("Api")]
	public IActionResult File(Guid id, string? folder, string file, bool includeFonts = false)
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

		if (System.IO.File.Exists(path))
		{
			// The only reason for this is for clients that might not support loading fonts over HTTP/S
			// *cough* libass-android *cough*
			// No shame toward the developers, fonts are a mess to work with anyway.
			// However, this MUST be used ONLY as a fallback for when the renderer doesn't take URLs as fonts
			if (mime is "text/x-ass" or "text/x-ssa" && includeFonts)
			{
				string attachmentsPath = Path.Join(video.Library.Path, video.Id.ToString(), "attachments");
				if (!Directory.Exists(attachmentsPath)) return PhysicalFile(path, mime);
				string[] fontFiles = Directory.GetFiles(attachmentsPath);

				if (fontFiles.Length <= 0) return PhysicalFile(path, mime);
				
				StringBuilder sb = new(System.IO.File.ReadAllText(path));
				sb.Append("\n[Fonts]\n");
				foreach (string fontFile in fontFiles)
				{
					sb.Append("fontname: ");
					sb.Append(Path.GetFileName(fontFile).ToLower());
					sb.Append('\n');

					byte[] data = System.IO.File.ReadAllBytes(fontFile);
					int linePos = 0;
					for (int i = 0; i < data.Length; i += 3)
					{
						int remaining = data.Length - i;
						uint chunk = (uint)data[i] << 16;
						if (remaining > 1) chunk |= (uint)data[i + 1] << 8;
						if (remaining > 2) chunk |= (uint)data[i + 2];

						int outChars = remaining switch { 1 => 2, 2 => 3, _ => 4 };
						for (int j = 0; j < outChars; j++)
						{
							sb.Append((char)(((chunk >> (18 - j * 6)) & 0x3F) + 33));
							if (++linePos != 80) continue;
							sb.Append('\n');
							linePos = 0;
						}
					}
					if (linePos > 0) sb.Append('\n');
					sb.Append('\n');
				}

				return Content(sb.ToString(), mime);
			}
			return PhysicalFile(path, mime);
		}
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