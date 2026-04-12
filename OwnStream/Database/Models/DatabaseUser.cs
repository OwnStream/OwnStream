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
			new(ClaimTypes.Name, Username),
			new(ClaimTypes.AuthenticationMethod, "JWT_Cookie"),
			new("OwnStream__userObject", JsonSerializer.Serialize(this)),
		];

		ClaimsIdentity identity = new(claims, "Cookies");
		return new ClaimsPrincipal(identity);
	}
}

[Flags]
public enum UserPermissions
{
	None = 0,
	Admin = 65535
}