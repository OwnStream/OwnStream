using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

public class VideoController(DatabaseContext db) : Controller
{
	public IActionResult Index() => View(db.Videos.Where(x => x.EpisodeId == null).ToArray());

	public IActionResult Watch(Guid id)
	{
		DatabaseVideo? video = db.Videos.Find(id);
		if (video == null) return RedirectToAction(nameof(Index));
		return View(video);
	}
}