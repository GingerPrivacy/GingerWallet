using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

// HDFingerprint is a struct, so need to use HDFingerprint? at the converter to be reference type
public class HDFingerprintJsonConverter : JsonConverter<HDFingerprint?>
{
	public override HDFingerprint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? new HDFingerprint(Convert.FromHexString(str)) : null;
	}

	public override void Write(Utf8JsonWriter writer, HDFingerprint? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
