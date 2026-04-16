using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OwnStream.JsonConverter;

public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? value = reader.GetString();

		if (string.IsNullOrWhiteSpace(value))
			return default;

		return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal)
			.ToUniversalTime();
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToUniversalTime()
			.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
	}
}