using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class HDFingerprintJsonConverter : JsonConverter<HDFingerprint?>
{
	/// <inheritdoc />
	public override HDFingerprint? ReadJson(JsonReader reader, Type objectType, HDFingerprint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var s = reader.Value as string;
		return !string.IsNullOrWhiteSpace(s) ? new HDFingerprint(Convert.FromHexString(s)) : null;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, HDFingerprint? value, JsonSerializer serializer)
	{
		var stringValue = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(stringValue);
	}
}
