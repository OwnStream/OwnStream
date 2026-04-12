using System.Text;
using Konscious.Security.Cryptography;

namespace OwnStream;

public static class Utils
{
	public static string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
		Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? defaultValue;

	public static byte[] GetPasswordHash(string username, string password)
	{
		Argon2id argon2Id = new(Encoding.UTF8.GetBytes(password));
		argon2Id.Iterations = 2;
		argon2Id.MemorySize = 1024;
		argon2Id.DegreeOfParallelism = 1;
		argon2Id.Salt = Encoding.UTF8.GetBytes(username);
		return argon2Id.GetBytes(32);
	}
}