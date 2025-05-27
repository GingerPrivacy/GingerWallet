using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class PubKeyJsonConverter : JsonConverter<PubKey>
{
	public override PubKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string str = reader.GetString().SafeTrim();
		return str is not null ? new PubKey(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, PubKey? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToHex());
	}
}
