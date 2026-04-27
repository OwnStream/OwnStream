using OwnStream.Database.Models;

namespace OwnStream.ApiModels;

public class User(DatabaseUser user)
{
	public Guid Id { get; set; } = user.Id;
	public string Username { get; set; } = user.Username;

	public string[] Permissions { get; set; } = user.Permissions != UserPermissions.None
		? Utils.GetAllPermissionsAsStringArray(user.Permissions)
		: [];
}