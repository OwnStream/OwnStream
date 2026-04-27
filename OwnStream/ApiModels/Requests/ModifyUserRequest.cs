namespace OwnStream.ApiModels.Requests;

public class ModifyUserRequest
{
	public string? Username { get; set; }
	public string? Password { get; set; }
	public List<string>? Permissions { get; set; }
}