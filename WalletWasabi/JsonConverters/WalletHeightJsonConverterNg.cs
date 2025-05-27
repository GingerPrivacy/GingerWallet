using GingerCommon.Static;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class WalletHeightJsonConverterNg : JsonConverter<Height>
{
	public override Height Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString().SafeTrim();

		return str.Length > 0 ? new Height((int)long.Parse(str, CultureInfo.InvariantCulture)) : throw new ArgumentNullException(nameof(str));
	}

	public override void Write(Utf8JsonWriter writer, Height value, JsonSerializerOptions options)
	{
		var safeHeight = Math.Max(0, value.Value - 101 /* maturity */);
		writer.WriteStringValue(safeHeight.ToString(CultureInfo.InvariantCulture));
	}
}
