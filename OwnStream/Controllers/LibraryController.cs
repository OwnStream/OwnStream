using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

[Authorize]
public class LibraryController(ILogger<LibraryController> logger, DatabaseContext db) : Controller
{
	public IActionResult Index() => View(db.Libraries.ToArray());

	[HttpGet]
	public IActionResult Create() => View();

	[HttpPost]
	public IActionResult Create([FromForm] string name, [FromForm] string path)
	{
		if (!Directory.Exists(path))
			return BadRequest("Path does not exist");

		try
		{
			string testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}");
			System.IO.File.WriteAllText(testFile, "test");
			System.IO.File.Delete(testFile);
			Directory.CreateDirectory(testFile);
			Directory.Delete(testFile);
		}
		catch
		{
			return BadRequest("Path is not writeable");
		}

		DatabaseLibrary library = new()
		{
			Id = Guid.NewGuid(),
			Name = name,
			Path = path
		};
		db.Add(library);
		db.SaveChanges();

		return RedirectToAction(nameof(Detail), new { id = library.Id });
	}

	public IActionResult Detail(Guid id)
	{
		DatabaseLibrary? library = db.Libraries.Find(id);
		if (library == null) return RedirectToAction(nameof(Index));
		return View(library);
	}
}