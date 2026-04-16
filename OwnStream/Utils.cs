using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Primitives;

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

	extension(Dictionary<string, string> translated)
	{
		public string? GetLocalized(string? original, HttpContext? context)
		{
			string[] headerLocales = context?.Request.Headers.AcceptLanguage
				.SelectMany(x => x?.Split(',').Select(l => l.Split(';')[0]) ?? []).ToArray() ?? [];
			StringValues queryLocales = [];
			context?.Request.Query.TryGetValue("locale", out queryLocales);
			string?[] locales = queryLocales.Count > 0
				? queryLocales.ToArray()
				: headerLocales;
			return translated.GetLocalized(original, locales);
		}

		private string? GetLocalized(string? original, params string?[] locales)
		{
			foreach (string? locale in locales)
			{
				if (locale == null) continue;

				string? translatedValue = translated
					.FirstOrDefault(x =>
						string.Equals(x.Key, locale, StringComparison.OrdinalIgnoreCase))
					.Value?.Trim();
				if (translatedValue?.Length > 0)
					return translatedValue;

				int separator = locale.IndexOfAny(['_', '-']);
				string localeFirstPart = separator <= 0 ? locale : locale[..separator];
				string? key = translated.Keys.FirstOrDefault(x =>
					x.StartsWith($"{localeFirstPart}_", StringComparison.InvariantCultureIgnoreCase) ||
					x.StartsWith($"{localeFirstPart}-", StringComparison.InvariantCultureIgnoreCase));
				string? value = key != null ? translated[key].Trim() : null;
				if (value?.Length > 0) return value;
			}

			return original;
		}
	}
}