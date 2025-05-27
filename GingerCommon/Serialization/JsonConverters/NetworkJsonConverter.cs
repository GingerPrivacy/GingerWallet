using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class NetworkJsonConverter : JsonConverter<Network>
{
	public override Network? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? Network.GetNetwork(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, Network? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
