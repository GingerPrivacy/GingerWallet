using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.JsonConverters;

public class PaymentDestinationJsonConverter : JsonConverter<IDestination>
{
	public override IDestination? ReadJson(JsonReader reader, Type objectType, IDestination? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is not string text || string.IsNullOrWhiteSpace(text))
		{
			throw new JsonSerializationException("A payment destination is required.");
		}
		foreach (var network in new[] { Network.Main, Network.TestNet, Network.RegTest })
		{
			try { return SilentPaymentAddress.Parse(text, network); }
			catch (Exception ex) when (ex is FormatException or ArgumentException) { }
		}
		return NBitcoinHelpers.BetterParseBitcoinAddress(text);
	}

	public override void WriteJson(JsonWriter writer, IDestination? value, JsonSerializer serializer) => writer.WriteValue(value?.ToString());
}
