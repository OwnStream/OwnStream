using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

public class UsersController(DatabaseContext db) : Controller
{
	[Authorize(Roles = nameof(UserPermissions.ReadAllUsers))]
	public IActionResult Index() => View(db.Users.ToArray());

	[HttpGet, Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public IActionResult Add() => View();

	[HttpPost, Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public IActionResult Add(string username, string password)
	{
		if (username.Length == 0 || password.Length == 0) return RedirectToAction("Add");

		DatabaseUser user = new()
		{
			Id = Guid.NewGuid(),
			Username = username,
			PasswordHash = Utils.GetPasswordHash(username, password),
			Permissions = UserPermissions.None
		};
		db.Users.Add(user);
		db.SaveChanges();
		return RedirectToAction("Detail", new { id = user.Id });
	}

	[HttpGet, Authorize(Roles = nameof(UserPermissions.ReadAllUsers))]
	public IActionResult Detail(Guid id)
	{
		DatabaseUser? user = db.Users.Find(id);
		if (user == null) return NotFound();
		return View(user);
	}

	[HttpGet, Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public IActionResult Delete(Guid id)
	{
		bool hasUser = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", out Guid userId);
		if (!hasUser) return Unauthorized();
		if (id == userId) return BadRequest("You cannot delete yourself!");

		DatabaseUser? user = db.Users.Find(id);
		if (user == null) return NotFound();
		db.Users.Remove(user);
		db.SaveChanges();

		return RedirectToAction("Index");
	}
}