using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels;
using OwnStream.ApiModels.Requests;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/manage/users/"), EnableCors("Api"),
 Authorize(Roles = nameof(UserPermissions.ReadAllUsers))]
// TODO: Rework requests/responses here
public class UsersController(DatabaseContext db) : Controller
{
	[HttpGet("list")]
	public User[] GetAllUsers()
	{
		return db.Users
			.OrderByDescending(x => x.Permissions)
			.Select(x => new User(x))
			.ToArray();
	}

	[HttpGet("{id:guid}")]
	public User? GetUser(Guid id)
	{
		DatabaseUser? user = db.Users.Find(id);
		if (user == null)
		{
			Response.StatusCode = (int)HttpStatusCode.NotFound;
			return null;
		}

		return new User(user);
	}

	[HttpPost("{id:guid}"), Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public User? ModifyUser(Guid id, [FromBody] ModifyUserRequest request)
	{
		DatabaseUser? currentUser =
			db.Users.Find(Guid.Parse(User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value));
		DatabaseUser? user = db.Users.Find(id);
		if (user == null || currentUser == null)
		{
			Response.StatusCode = (int)HttpStatusCode.NotFound;
			return null;
		}

		user.Username = request.Username ?? user.Username;
		if (request.Password != null)
			user.PasswordHash = Utils.GetPasswordHash(user.Username, request.Password);

		if (request.Permissions != null && currentUser.Id != user.Id)
		{
			request.Permissions.Remove("Owner");
			user.Permissions = Enum.GetValues<UserPermissions>()
				.Where(permission => request.Permissions.Contains(permission.ToString()) &&
				                     currentUser.Permissions.HasFlag(permission))
				.Aggregate(UserPermissions.None, (current, permission) => current | permission);
		}

		db.SaveChanges();

		return new User(user);
	}

	[HttpDelete("{id:guid}"), Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public IActionResult? DeleteUser(Guid id)
	{
		DatabaseUser? user = db.Users.Find(id);
		if (user == null)
		{
			Response.StatusCode = (int)HttpStatusCode.NotFound;
			return null;
		}

		db.Users.Remove(user);
		db.SaveChanges();

		return Json(new { });
	}

	[HttpPost("new"), Authorize(Roles = nameof(UserPermissions.WriteUsers))]
	public User? CreateUser([FromBody] CreateUserRequest request)
	{
		int existing = db.Users.Count(x => x.Username == request.Username);
		if (existing != 0)
		{
			Response.StatusCode = (int)HttpStatusCode.Conflict;
			return null;
		}

		DatabaseUser newUser = new()
		{
			Id = Guid.NewGuid(),
			Username = request.Username,
			PasswordHash = Utils.GetPasswordHash(request.Username, request.Password),
			Permissions = UserPermissions.None
		};
		db.Users.Add(newUser);
		db.SaveChanges();
		return new User(newUser);
	}

	[HttpPost("setupNew"), AllowAnonymous]
	public User? CreateInitialUser([FromBody] CreateUserRequest request)
	{
		if (db.IsSetup())
		{
			Response.StatusCode = (int)HttpStatusCode.NotFound;
			return null;
		}

		int existing = db.Users.Count(x => x.Username == request.Username);
		if (existing != 0)
		{
			Response.StatusCode = (int)HttpStatusCode.Conflict;
			return null;
		}

		DatabaseUser newUser = new()
		{
			Id = Guid.NewGuid(),
			Username = request.Username,
			PasswordHash = Utils.GetPasswordHash(request.Username, request.Password),
			Permissions = UserPermissions.Owner
		};
		db.Users.Add(newUser);
		db.SaveChanges();
		return new User(newUser);
	}
}