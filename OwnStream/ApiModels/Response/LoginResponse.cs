namespace OwnStream.ApiModels.Response;

public class LoginResponse
{
	public bool Success { get; }
	public string? Message { get; set; }
	public string? Username { get; set; }
	public string? AccessToken { get; set; }

	public LoginResponse(string message)
	{
		Success = false;
		Message = message;
		Username = null;
		AccessToken = null;
	}

	public LoginResponse(string username, string token)
	{
		Success = true;
		Message = null;
		Username = username;
		AccessToken = token;
	}
}