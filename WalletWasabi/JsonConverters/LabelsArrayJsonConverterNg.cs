using GingerCommon.Static;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.JsonConverters;

public class LabelsArrayJsonConverterNg : JsonConverter<LabelsArray>
{
	public override LabelsArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return new LabelsArray(reader.GetString().SafeTrim());
	}

	public override void Write(Utf8JsonWriter writer, LabelsArray value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value!.ToString());
	}
}
