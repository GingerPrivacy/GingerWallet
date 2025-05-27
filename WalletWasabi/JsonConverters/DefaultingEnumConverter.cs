using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters;

public class DefaultingEnumConverter<TEnum> : JsonConverter<TEnum>
	where TEnum : struct, Enum
{
	private readonly bool _ignoreCase;
	private readonly TEnum _fallbackValue;

	/// <summary>
	/// Fallbacks to default(TEnum), ignores case.
	/// </summary>
	public DefaultingEnumConverter()
		: this(defaultValue: default, ignoreCase: true)
	{
	}

	/// <summary>
	/// Fallbacks to default(TEnum) or a specific value, and optionally ignores case.
	/// </summary>
	/// <param name="defaultValue">
	///   The enum value to return on null/empty/unparseable input.
	/// </param>
	/// <param name="ignoreCase">
	///   Whether to do a case‐insensitive parse.
	/// </param>
	public DefaultingEnumConverter(TEnum defaultValue, bool ignoreCase = true)
	{
		_fallbackValue = defaultValue;
		_ignoreCase = ignoreCase;
	}

	public override TEnum Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		// If the JSON is literally `null`
		if (reader.TokenType == JsonTokenType.Null)
		{
			return _fallbackValue;
		}

		// We only handle strings
		if (reader.TokenType != JsonTokenType.String)
		{
			reader.Skip();
			return _fallbackValue;
		}

		var s = reader.GetString();
		if (string.IsNullOrWhiteSpace(s))
		{
			return _fallbackValue;
		}

		if (Enum.TryParse<TEnum>(s!, _ignoreCase, out var parsed))
		{
			return parsed;
		}

		// Unrecognized → fallback
		return _fallbackValue;
	}

	public override void Write(
		Utf8JsonWriter writer,
		TEnum value,
		JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}
