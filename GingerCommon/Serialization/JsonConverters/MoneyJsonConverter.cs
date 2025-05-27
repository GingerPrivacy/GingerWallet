using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class MoneyJsonConverter : JsonConverter<Money>
{
	public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? Money.Parse(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, Money? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString(false, true));
	}
}
