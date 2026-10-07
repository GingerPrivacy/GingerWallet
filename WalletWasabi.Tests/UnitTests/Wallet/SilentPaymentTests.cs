using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NBitcoin;
using NBitcoin.DataEncoders;
using WalletWasabi.Wallets.SilentPayment;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SilentPaymentTests
{
	// Sender portions of BIP352 v1.1.1 official vectors, commit c2ac36f48f71615984087fd151f410457edfed72.
	// https://github.com/bitcoin/bips/blob/c2ac36f48f71615984087fd151f410457edfed72/bip-0352/send_and_receive_test_vectors.json
	[Theory]
	[MemberData(nameof(TestCases))]
	public void OfficialSenderVectors(string description, string vectorJson)
	{
		using var document = JsonDocument.Parse(vectorJson);
		foreach (var test in document.RootElement.GetProperty("sending").EnumerateArray())
		{
			var given = test.GetProperty("given");
			var inputs = given.GetProperty("vin").EnumerateArray().Select(input =>
			{
				var script = Script.FromHex(input.GetProperty("prevout").GetProperty("scriptPubKey").GetProperty("hex").GetString()!);
				var scriptSig = Script.FromHex(input.GetProperty("scriptSig").GetString()!);
				var witnessText = input.GetProperty("txinwitness").GetString()!;
				var witness = witnessText.Length == 0 ? WitScript.Empty : new WitScript(Encoders.Hex.DecodeData(witnessText));
				var eligible = SilentPayment.ExtractPubKey(scriptSig, witness, script) is not null;
				return new Utxo(new OutPoint(uint256.Parse(input.GetProperty("txid").GetString()!), input.GetProperty("vout").GetUInt32()),
					new Key(Encoders.Hex.DecodeData(input.GetProperty("private_key").GetString()!)), script, eligible);
			}).ToArray();
			try
			{
				var recipients = given.GetProperty("recipients").EnumerateArray().SelectMany(recipient =>
					Enumerable.Repeat(SilentPaymentAddress.Parse(recipient.GetProperty("address").GetString()!, Network.Main),
						recipient.TryGetProperty("count", out var count) ? count.GetInt32() : 1)).ToArray();
				var expected = test.GetProperty("expected").GetProperty("outputs").EnumerateArray()
					.Select(set => set.EnumerateArray().Select(key => key.GetString()!).ToHashSet()).ToArray();
				if (description.Contains("sending fails", StringComparison.Ordinal))
				{
					Assert.ThrowsAny<ArgumentException>(() => SilentPayment.GetPubKeys(recipients, inputs));
					continue;
				}
				var actual = SilentPayment.GetPubKeys(recipients, inputs).SelectMany(p => p.Value)
					.Select(key => Encoders.Hex.EncodeData(key.ToBytes())).ToHashSet();
				if (expected.Length == 0) { Assert.Empty(actual); }
				else { Assert.Contains(expected, set => set.SetEquals(actual)); }
			}
			finally { foreach (var input in inputs) { input.SigningKey.Dispose(); } }
		}
	}

	public static IEnumerable<object[]> TestCases
	{
		get
		{
			using var document = JsonDocument.Parse(File.ReadAllText("./UnitTests/Data/SilentPaymentTestVectors.json"));
			return document.RootElement.EnumerateArray().Select(test => new object[] { test.GetProperty("comment").GetString()!, test.GetRawText() }).ToArray();
		}
	}
}
