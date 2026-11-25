using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Primitives;
using OwnStream.Database.Models;

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

	public static string[] GetAllPermissionsAsStringArray(UserPermissions userPermissions)
	{
		return (from permission in Enum.GetValues<UserPermissions>()
				where permission != UserPermissions.None
				where userPermissions.HasFlag(permission)
				select permission.ToString()
			).ToArray();
	}

	public static string RemoveInvalidFileNameChars(this string? fileName, char? replaceChar = '_')
	{
		if (fileName == null) return "";
		foreach (char c in Path.GetInvalidFileNameChars())
			fileName = fileName.Replace(c.ToString(), replaceChar?.ToString() ?? "");

		return fileName
			// for some reason (at least on linux) " is not an invalid file name char
			// i mean it probably is valid but it breaks stuff so im testing it here too
			.Replace("\"", replaceChar?.ToString() ?? "")
			// https://github.com/OwnStream/OwnStream/issues/19
			.Replace("[", replaceChar?.ToString() ?? "")
			.Replace("]", replaceChar?.ToString() ?? "");
	}

	public static TValue? FirstNonEmptyValue<TItem, TValue>(this IEnumerable<TItem> items, Func<TItem, TValue> func,
		StringType stringType = StringType.Default)
	{
		foreach (TItem item in items)
		{
			TValue val = func.Invoke(item);
			switch (val)
			{
				case string sVal:
					if (!string.IsNullOrWhiteSpace(sVal))
					{
						switch (stringType)
						{
							case StringType.Date:
								if (TryParseIsoString(sVal, out DateTimeOffset _))
									return val;
								break;
							default:
								return val;
						}
					}

					break;
				default:
					if (val != null)
						return val;
					break;
			}
		}

		return default;
	}

	public static IEnumerable<TValue> CombineLists<TItem, TValue>(this IEnumerable<TItem>
		items, Func<TItem, IEnumerable<TValue>> func) => items.SelectMany(func);

	public static Dictionary<TKey, TValue> CombineDicts<TItem, TKey, TValue>(this IEnumerable<TItem> items,
		Func<TItem, Dictionary<TKey, TValue>> func, Dictionary<TKey, TValue>? baseValue = null,
		TValue? ignoreIf = default) where TKey : notnull
	{
		Dictionary<TKey, TValue> result = [];
		foreach (TItem item in items)
		{
			Dictionary<TKey, TValue> val = func.Invoke(item);
			foreach ((TKey key, TValue value) in val)
				if (!EqualityComparer<TValue>.Default.Equals(value, ignoreIf))
					result.TryAdd(key, value);
		}

		foreach ((TKey key, TValue value) in baseValue ?? [])
			if (!EqualityComparer<TValue>.Default.Equals(value, ignoreIf))
				result.TryAdd(key, value);

		return result;
	}

	public enum StringType
	{
		Default,
		Date
	}

	private static readonly string[] IsoFormats =
	{
		"yyyy-MM-ddTHH:mm:ss.fffffffzzz",
		"yyyy-MM-ddTHH:mm:sszzz",
		"yyyy-MM-ddTHH:mm:ss'Z'",
		"yyyy-MM-ddTHH:mm:ss",
		"yyyy-MM-dd"
	};

	public static bool TryParseIsoString(string? input, out DateTimeOffset result)
	{
		result = default;
		if (input == null) return false;
		return DateTimeOffset.TryParseExact(
			input,
			IsoFormats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out result);
	}

	// i dont think this method belongs here but oh well
	public static string? Which(string name)
	{
		if (Path.IsPathRooted(name) && File.Exists(name))
			return Path.GetFullPath(name);

		string? pathEnv = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrEmpty(pathEnv))
			return null;

		string[] directories = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

		// Windows uses PATHEXT to know which file extensions are executable.
		string[] extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? GetPathextExtensions()
			: new[] { string.Empty };

		foreach (string directory in directories)
		{
			foreach (string ext in extensions)
			{
				string candidate = Path.Combine(directory, name + ext);
				if (!File.Exists(candidate))
					continue;

				// On Unix-like systems, prefer files that have at least one execute bit.
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsExecutable(candidate))
					continue;

				return Path.GetFullPath(candidate);
			}
		}

		return null;
	}

	private static string[] GetPathextExtensions()
	{
		string? pathext = Environment.GetEnvironmentVariable("PATHEXT");
		if (string.IsNullOrEmpty(pathext))
			return new[] { ".exe", ".cmd", ".bat", ".com" };

		return pathext.Split(';', StringSplitOptions.RemoveEmptyEntries)
			.Select(e => e.StartsWith('.') ? e : "." + e)
			.ToArray();
	}

	[UnsupportedOSPlatform("windows")]
	private static bool IsExecutable(string path)
	{
		try
		{
			UnixFileMode mode = File.GetUnixFileMode(path);
			return (mode & UnixFileMode.UserExecute) != 0 ||
			       (mode & UnixFileMode.GroupExecute) != 0 ||
			       (mode & UnixFileMode.OtherExecute) != 0;
		}
		catch
		{
			return false;
		}
	}
}