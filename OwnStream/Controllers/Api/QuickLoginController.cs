using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/auth/remote"), EnableCors("Api")]
public class QuickLoginController(DatabaseContext db) : Controller
{
	private static List<QuickLoginSession> sessions = [];

	[HttpGet("start")]
	public QuickLoginStartResponse StartQuickLogin([FromQuery] string deviceName)
	{
		char[] code = Encoding.UTF8.GetChars("------"u8.ToArray());
		do
		{
			for (int i = 0; i < code.Length; i++)
			{
				code[i] = (char)(Random.Shared.Next(0, 10) + 0x30);
			}
		} while (sessions.Any(x => x.Code == new string(code)));

		QuickLoginSession newSession = new()
		{
			DeviceName = deviceName,
			Token = Random.Shared.GetHexString(32, true),
			Code = new string(code),
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
		};
		sessions.Add(newSession);
		return new QuickLoginStartResponse
		{
			DeviceName = deviceName,
			Token = newSession.Token,
			Code = newSession.Code
		};
	}

	[HttpGet("check")]
	public QuickLoginCheckResponse CheckQuickLogin([FromQuery] string token)
	{
		QuickLoginSession? session = sessions.FirstOrDefault(x => x.Token == token);
		if (session == null)
			return new QuickLoginCheckResponse
			{
				TokenValid = false,
				LoginComplete = false,
				SignInResult = null
			};

		if (session.ExpiresAt < DateTimeOffset.UtcNow)
		{
			sessions.Remove(session);
			return new QuickLoginCheckResponse
			{
				TokenValid = false,
				LoginComplete = false,
				SignInResult = null
			};
		}

		DatabaseUser? user = session.UserId != null ? db.Users.Find(session.UserId) : null;
		if (user != null)
		{
			sessions.Remove(session);
			return new QuickLoginCheckResponse
			{
				TokenValid = true,
				LoginComplete = true,
				SignInResult = JwtAuth.GetToken(user.GetPrincipal(),
					Convert.FromHexString(Utils.GetEnvironmentVariable("JWT_KEY")!))
			};
		}

		return new QuickLoginCheckResponse
		{
			TokenValid = true,
			LoginComplete = false,
			SignInResult = null
		};
	}

	[HttpGet("authorize"), Authorize]
	public QuickLoginAuthorizeResponse AuthorizeQuickLogin([FromQuery] string code, [FromQuery] Guid? asUser,
		[FromQuery] string? deviceNameHash)
	{
		QuickLoginSession? session = sessions.FirstOrDefault(x => x.Code == code);

		if (session == null)
			return new QuickLoginAuthorizeResponse
			{
				TokenValid = false,
				DeviceName = null,
				SignedIn = false
			};

		if (session.ExpiresAt < DateTimeOffset.UtcNow || session.UserId != null)
		{
			sessions.Remove(session);
			return new QuickLoginAuthorizeResponse
			{
				TokenValid = false,
				DeviceName = null,
				SignedIn = false
			};
		}

		Guid signedInUserId = Guid.Parse(User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value);
		DatabaseUser? user = db.Users.Find(signedInUserId);

		if (user == null)
			return new QuickLoginAuthorizeResponse
			{
				TokenValid = false,
				DeviceName = session.DeviceName,
				SignedIn = false
			};

		if (deviceNameHash == null)
			return new QuickLoginAuthorizeResponse
			{
				TokenValid = true,
				DeviceName = session.DeviceName,
				SignedIn = false
			};

		if ((user.Permissions & UserPermissions.WriteUsers) != UserPermissions.None && asUser != null)
		{
			DatabaseUser? signingInAsUser = db.Users.Find(asUser);

			if (signingInAsUser == null)
				return new QuickLoginAuthorizeResponse
				{
					TokenValid = false,
					DeviceName = session.DeviceName,
					SignedIn = false
				};
			user = signingInAsUser;
		}

		byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(session.DeviceName));
		string hashString = Convert.ToHexString(hash);
		if (!hashString.Equals(deviceNameHash, StringComparison.OrdinalIgnoreCase))
		{
			return new QuickLoginAuthorizeResponse
			{
				TokenValid = false,
				DeviceName = null,
				SignedIn = false
			};
		}

		session.UserId = user.Id;
		return new QuickLoginAuthorizeResponse
		{
			TokenValid = true,
			DeviceName = session.DeviceName,
			SignedIn = true
		};
	}
	
	[HttpGet("sessions"), Authorize(Roles = nameof(UserPermissions.Admin))]
	public List<QuickLoginSession> GetSessions()
	{
		sessions.RemoveAll(x => x.ExpiresAt < DateTimeOffset.UtcNow);
		return sessions;
	}

	public class QuickLoginSession
	{
		public string DeviceName { get; set; }
		[JsonIgnore] public string Token { get; set; }
		public string Code { get; set; }
		public DateTimeOffset ExpiresAt { get; set; }
		[JsonIgnore] public Guid? UserId { get; set; }
	}
}