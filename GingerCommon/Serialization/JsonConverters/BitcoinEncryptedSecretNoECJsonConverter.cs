using GingerCommon.Static;
using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GingerCommon.Serialization.JsonConverters;

public class BitcoinEncryptedSecretNoECJsonConverter : JsonConverter<BitcoinEncryptedSecretNoEC>
{
	public override BitcoinEncryptedSecretNoEC? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		// The `network` is required but the encoding doesn't depend on it.
		return str.Length > 0 ? new BitcoinEncryptedSecretNoEC(str, Network.Main) : null;
	}

	public override void Write(Utf8JsonWriter writer, BitcoinEncryptedSecretNoEC? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToWif());
	}
}
