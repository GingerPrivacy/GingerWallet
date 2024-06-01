using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert URIs to and from JSON.
/// </summary>
public class TestNetBackendUriJsonConverter : JsonConverter<string>
{
	public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return Constants.TestnetBackendUri;
	}

	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}
}
