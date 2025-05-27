using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class Uint256JsonConverter : JsonConverter<uint256>
{
	public override uint256? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? new uint256(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, uint256? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
