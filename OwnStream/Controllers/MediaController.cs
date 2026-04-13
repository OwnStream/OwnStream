using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;

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
}