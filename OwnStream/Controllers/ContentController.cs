using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

[Authorize]
public class ContentController(DatabaseContext db) : Controller
{
	public IActionResult Index() => View(db.Content.OrderBy(x => x.CreatedAt).ToArray());
	public IActionResult Details(Guid id)
	{
		DatabaseContent? webhook = db.Content
			.Include(x => x.Episodes)
			.ThenInclude(x => x.Videos)
			.FirstOrDefault(x => x.Id == id);
		if (webhook == null) return RedirectToAction(nameof(Index));
		return View(webhook);
	}
}