using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/auth"), EnableCors("Api")]
public class AuthController(DatabaseContext db) : Controller
{
	[HttpPost("login")]
	public IActionResult Login([FromBody] LoginRequest request)
	{
		DatabaseUser? user = db.Users.FirstOrDefault(x => x.Username == request.Username);

		if (user == null || !user.PasswordHash.SequenceEqual(Utils.GetPasswordHash(request.Username, request.Password)))
			return Json(new LoginResponse("Invalid username or password"));

		ClaimsPrincipal principal = user.GetPrincipal();
		return SignIn(principal, "ApiToken");
	}

	[HttpGet("whoami"), Authorize]
	public User WhoAmI()
	{
		DatabaseUser? user =
			db.Users.Find(Guid.Parse(User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value));
		return new User(user!);
	}
}