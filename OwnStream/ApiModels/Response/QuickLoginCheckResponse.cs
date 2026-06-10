using Microsoft.AspNetCore.Mvc;

namespace OwnStream.ApiModels.Response;

public class QuickLoginCheckResponse
{
	public bool TokenValid { get; set; }
	public bool LoginComplete { get; set; }
	public string? SignInResult { get; set; }
}