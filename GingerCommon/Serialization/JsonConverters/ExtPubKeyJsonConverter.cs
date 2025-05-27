using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class ExtPubKeyJsonConverter : JsonConverter<ExtPubKey>
{
	public override ExtPubKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? extPubKeyString = reader.GetString();
		return extPubKeyString is not null ? NBitcoinUtils.ParseExtPubKey(extPubKeyString) : null;
	}

	public override void Write(Utf8JsonWriter writer, ExtPubKey? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.GetWif(Network.Main).ToWif());
	}
}
