namespace OwnStream.ApiModels.Response;

public class SuccessResponse(bool success, string? message = null)
{
	public bool Success { get; set; } = success;
	public string? Message { get; set; } = message;
}

public class SuccessResponse<T>(bool success, string? message = null, T? value = default)
	: SuccessResponse(success, message)
{
	public T? Data { get; set; } = value;
}