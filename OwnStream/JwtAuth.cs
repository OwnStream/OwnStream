using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream;

public class JwtAuth(IOptionsMonitor<JwtAuth.SchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
	: SignInAuthenticationHandler<JwtAuth.SchemeOptions>(options,
		logger, encoder)
{
	private static readonly IJwtAlgorithm Algorithm = new HMACSHA256Algorithm();
	private static readonly IBase64UrlEncoder UrlEncoder = new JwtBase64UrlEncoder();
	private static readonly IJsonSerializer Serializer = new SystemTextSerializer();
	private static readonly IDateTimeProvider Provider = new UtcDateTimeProvider();
	private static readonly IJwtValidator Validator = new JwtValidator(Serializer, Provider);
	private static readonly IJwtEncoder Encoder = new JwtEncoder(Algorithm, Serializer, UrlEncoder);
	private static readonly IJwtDecoder Decoder = new JwtDecoder(Serializer, Validator, UrlEncoder, Algorithm);

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		string header = Context.Request.Headers.Authorization.FirstOrDefault() ?? "";
		if (!header.StartsWith("Bearer ", StringComparison.InvariantCultureIgnoreCase))
			return AuthenticateResult.NoResult();

		string token = header["Bearer ".Length..];
		try
		{
			JsonObject jwt = Decoder.DecodeToObject<JsonObject>(token, Options.JwtKey);
			string? idString = jwt["sub"]?.GetValue<string>();
			Guid? id = Guid.TryParse(idString, out Guid g) ? g : null;

			DatabaseContext db = Context.RequestServices.GetRequiredService<DatabaseContext>();
			DatabaseUser? user = await db.Users.FindAsync([id]);

			if (user == null) return AuthenticateResult.Fail("Invalid token");

			ClaimsPrincipal principal = user.GetPrincipal();
			return AuthenticateResult.Success(new AuthenticationTicket(principal, "ApiToken"));
		}
		catch (Exception e)
		{
			return AuthenticateResult.Fail(e);
		}
	}

	protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
	{
		return Task.CompletedTask;
	}

	protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
	{
		JsonObject jwtBody = new()
		{
			{ "iat", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
			{ "sub", user.FindFirst(ClaimTypes.NameIdentifier)?.Value },
			{ "name", user.FindFirst(ClaimTypes.Name)?.Value },
		};
		Response.StatusCode = 200;
		Response.ContentType = "application/json";
		Response.WriteAsJsonAsync(new LoginResponse(user.FindFirst(ClaimTypes.Name)?.Value ?? "", Encoder.Encode(jwtBody, Options.JwtKey)));
		return Task.CompletedTask;
	}

	public class SchemeOptions : AuthenticationSchemeOptions
	{
		public byte[] JwtKey { get; set; } = [];
	}
}