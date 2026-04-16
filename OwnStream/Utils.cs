using System.Runtime.CompilerServices;
using System.Text;
using Konscious.Security.Cryptography;

namespace OwnStream;

public static class Utils
{
	public static string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
		Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? defaultValue;

	public static byte[] GetPasswordHash(string username, string password)
	{
		try
		{
			Argon2id argon2Id = new(Encoding.UTF8.GetBytes(password));
			argon2Id.Iterations = 2;
			argon2Id.MemorySize = 1024;
			argon2Id.DegreeOfParallelism = 1;
			argon2Id.Salt = Encoding.UTF8.GetBytes(username);
			return argon2Id.GetBytes(32);
		}
		catch
		{
			return new byte[32];
		}
	}

	public static string ToString(this TimeSpan timeSpan, string h = "h", string m = "m")
	{
		StringBuilder sb = new();

		if (timeSpan.TotalHours >= 1) sb.Append(Math.Floor(timeSpan.TotalHours)).Append(h).Append(' ');
		if (timeSpan.Minutes > 0) sb.Append(timeSpan.Minutes).Append(m);
		else if (timeSpan.TotalSeconds < 60) sb.Append("< 1").Append(m);

		return sb.ToString();
	}
}