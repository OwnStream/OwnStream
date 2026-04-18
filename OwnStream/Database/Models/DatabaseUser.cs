using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OwnStream.Database.Models;

[Index(nameof(Username), IsUnique = true, Name = "User_Username")]
public class DatabaseUser
{
	public Guid Id { get; set; }
	public string Username { get; set; }
	public byte[] PasswordHash { get; set; }
	public UserPermissions Permissions { get; set; }

	public ClaimsPrincipal GetPrincipal()
	{
		List<Claim> claims =
		[
			new(ClaimTypes.NameIdentifier, Id.ToString()),
			new(ClaimTypes.Name, Username)
		];
		claims.AddRange(from permission in Enum.GetValues<UserPermissions>()
			where permission != UserPermissions.None && Permissions.HasFlag(permission)
			select new Claim(ClaimTypes.Role, permission.ToString()));

		ClaimsIdentity identity = new(claims, "Cookies");
		return new ClaimsPrincipal(identity);
	}
}

[Flags]
public enum UserPermissions
{
	None = 0,
	ReadJobs = 1 << 0,
	WriteJobs = 1 << 1,
	ReadWebhooks = 1 << 2,
	WriteWebhooks = 1 << 3,
	WriteContent = 1 << 4,
	WriteLibraries = 1 << 5,
	WriteVideos = 1 << 6,
	ReadAllUsers = 1 << 7,
	WriteUsers = 1 << 8,

	Admin = 0b1111111111111111,
	Owner = 0b11111111111111111
}