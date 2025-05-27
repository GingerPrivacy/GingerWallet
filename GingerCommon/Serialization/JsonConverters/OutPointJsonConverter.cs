using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class OutPointJsonConverter : JsonConverter<OutPoint>
{
	public override OutPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		if (str.Length > 0)
		{
			OutPoint res = new();
			res.FromBytes(Convert.FromHexString(str));
			return res;
		}
		return null;
	}

	public override void Write(Utf8JsonWriter writer, OutPoint? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(Convert.ToHexString(value!.ToBytes()));
	}
}
