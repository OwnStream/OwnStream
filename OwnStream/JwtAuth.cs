using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
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
		string? token = null;

		if (header.StartsWith("Bearer ", StringComparison.InvariantCultureIgnoreCase))
			token = header["Bearer ".Length..];
		else if (Context.Request.Query.TryGetValue("access_token", out StringValues queryToken))
			token = queryToken.FirstOrDefault();

		if (string.IsNullOrEmpty(token))
			return AuthenticateResult.NoResult();
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
		Response.StatusCode = 200;
		Response.ContentType = "application/json";
		Response.WriteAsJsonAsync(new LoginResponse(user.FindFirst(ClaimTypes.Name)?.Value ?? "", GetToken(user, Options.JwtKey)));
		return Task.CompletedTask;
	}

	internal static string GetToken(ClaimsPrincipal user, byte[] jwtKey)
	{
		JsonObject jwtBody = new()
		{
			{ "iat", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
			{ "sub", user.FindFirst(ClaimTypes.NameIdentifier)?.Value },
			{ "name", user.FindFirst(ClaimTypes.Name)?.Value },
		};
		return Encoder.Encode(jwtBody, jwtKey);
	}

	public class SchemeOptions : AuthenticationSchemeOptions
	{
		public byte[] JwtKey { get; set; } = [];
	}
}