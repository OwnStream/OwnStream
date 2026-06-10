namespace OwnStream.ApiModels.Response;

public class QuickLoginAuthorizeResponse
{
	public bool TokenValid { get; set; }
	public string? DeviceName { get; set; }
	public bool SignedIn { get; set; }
}