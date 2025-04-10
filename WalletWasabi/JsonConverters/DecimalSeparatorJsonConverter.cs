using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Extensions;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;

namespace WalletWasabi.JsonConverters;

public class DecimalSeparatorJsonConverter : JsonConverter<string>
{
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException("Unexpected token type for string property.");
		}

		var value = reader.GetString() ?? DecimalSeparator.Dot.GetChar();

		var allowedDecimalSeparators = Enum.GetValues(typeof(DecimalSeparator)).Cast<DecimalSeparator>().Select(x => x.GetChar()).ToArray();
		if (allowedDecimalSeparators.Contains(value))
		{
			return value;
		}

		return LocalizationExtension.GuessPreferredDecimalSeparator();
	}

	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}
}
