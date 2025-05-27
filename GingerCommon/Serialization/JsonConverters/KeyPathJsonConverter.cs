using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class KeyPathJsonConverter : JsonConverter<KeyPath>
{
	public override KeyPath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? KeyPath.Parse(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, KeyPath? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
