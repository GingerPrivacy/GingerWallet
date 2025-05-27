using GingerCommon.Static;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class CoinjoinSkipFactorsJsonConverterNg : JsonConverter<CoinjoinSkipFactors>
{
	public override CoinjoinSkipFactors? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();
		return str.Length > 0 ? CoinjoinSkipFactors.FromString(str) : null;
	}

	public override void Write(Utf8JsonWriter writer, CoinjoinSkipFactors? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
