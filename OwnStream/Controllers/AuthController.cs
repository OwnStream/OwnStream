using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers;

public class AuthController(DatabaseContext db) : Controller
{
	[HttpGet]
	public IActionResult Login()
	{
		if (!db.IsSetup()) return RedirectToAction("InitialSetup");
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> Login(string username, string password)
	{
		if (!db.IsSetup()) return RedirectToAction("InitialSetup");

		DatabaseUser? user = db.Users.FirstOrDefault(x => x.Username == username);

		if (user == null || !user.PasswordHash.SequenceEqual(Utils.GetPasswordHash(username, password)))
			return RedirectToAction("Login");

		ClaimsPrincipal principal = user.GetPrincipal();
		await HttpContext.SignInAsync("Cookies", principal);

		return Redirect("/");
	}

	[HttpGet]
	public IActionResult InitialSetup()
	{
		if (db.IsSetup()) return RedirectToAction("Login");
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> InitialSetup(string username, string password)
	{
		if (db.IsSetup()) return RedirectToAction("Login");
		
		if (username.Length == 0 || password.Length == 0) return RedirectToAction("InitialSetup");

		DatabaseUser user = new()
		{
			Id = Guid.NewGuid(),
			Username = username,
			PasswordHash = Utils.GetPasswordHash(username, password),
			Permissions = UserPermissions.Admin
		};
		db.Users.Add(user);
		await db.SaveChangesAsync();

		return RedirectToAction("Login");
	}
}