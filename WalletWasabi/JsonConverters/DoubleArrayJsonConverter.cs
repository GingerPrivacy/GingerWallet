using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WalletWasabi.JsonConverters;

public class DoubleArrayJsonConverter : JsonConverter<IEnumerable<double>>
{
	public override IEnumerable<double>? ReadJson(JsonReader reader, Type objectType, IEnumerable<double>? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		return Parse(stringValue);
	}

	public static IEnumerable<double>? Parse(string? stringValue)
	{
		if (stringValue is null)
		{
			return null;
		}
		else if (stringValue.Equals(string.Empty))
		{
			return Enumerable.Empty<double>();
		}

		return stringValue.Split(',').Select(str => double.Parse(str, CultureInfo.InvariantCulture));
	}

	public override void WriteJson(JsonWriter writer, IEnumerable<double>? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		var stringValue = string.Join(", ", value);
		writer.WriteValue(stringValue);
	}
}
