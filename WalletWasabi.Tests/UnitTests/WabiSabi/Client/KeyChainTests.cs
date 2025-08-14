using NBitcoin;
using NBitcoin.BIP322;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class KeyChainTests
{
	[Fact]
	public void SignTransactionTest()
	{
		var keyManager = KeyManager.CreateNew(out _, "", Network.Main);
		var destinationProvider = new InternalDestinationProvider(keyManager);
		var keyChain = new KeyChain(keyManager, new Kitchen(""));

		var coinDestination = destinationProvider.GetNextDestinations(1, false).First();
		var coin = new Coin(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1.0m), coinDestination));

		var transaction = Transaction.Create(Network.Main); // the transaction doesn't contain the input that we request to be signed.

		Assert.Throws<InvalidOperationException>(() => keyChain.Sign(transaction, coin, transaction.PrecomputeTransactionData()));

		transaction.Inputs.Add(coin.Outpoint);
		var signedTx = keyChain.Sign(transaction, coin, transaction.PrecomputeTransactionData(new[] { coin }));
		Assert.True(signedTx.HasWitness);
	}

	// --- Legacy (+8 header quirk) exact-match fixtures for index 0 and 1 ---
	[Theory]
	[InlineData(0, "helloworld", "H5bVLsJtPUeZiS+7N1GlMw6hTsLbUcLocs7qkeCzlxnmdYPBxk7NrgtYyd+NaNDvBW1ehOU69qSdohfZnFzI3X0=")]
	[InlineData(1, "helloworld", "IGC8vofPdhcLLSlVR2CCf35R6VJzYQknFjF6FR5zuKfnLXVcxopHBGD+4BL9JimiRUTlLzBGpF47oB7IP5hQnjI=")]
	public void SignMessage_LegacyPlus8_MultipleCases(int index, string message, string expectedBase64)
	{
		// Arrange wallet & helpers
		var keyManager = KeyManager.Recover(
			new Mnemonic("faculty plunge pilot slice amount salute artist raw amateur excite dial canyon"),
			"",
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit));

		var destinationProvider = new InternalDestinationProvider(keyManager);
		var keyChain = new KeyChain(keyManager, new Kitchen(""));
		var net = keyManager.GetNetwork();

		// Get HdPubKey at the requested external index
		var dests = destinationProvider.GetNextDestinations(index + 1, false).ToArray();
		var dest = dests.Last();
		Assert.True(keyManager.TryGetKeyForScriptPubKey(dest.ScriptPubKey, out HdPubKey? hdPubKey));
		Assert.NotNull(hdPubKey);

		// Act: your KeyChain.SignMessage uses Legacy + header +8, lowR:false
		var sig = keyChain.SignMessage(message, hdPubKey!);

		// Exact-match with the fixture you supplied
		Assert.Equal(expectedBase64, sig);

		// Sanity: recover pubkey from legacy compact sig & compare
		var hashLegacy = BIP322Signature.CreateMessageHash(message, legacy: true);
		var raw = Convert.FromBase64String(sig);
		Assert.Equal(65, raw.Length);
		int recId = (raw[0] - 27) & 3;
		var compact = new CompactSignature(recId, raw.AsSpan(1, 64).ToArray());
		var recovered = PubKey.RecoverCompact(hashLegacy, compact);
		Assert.Equal(hdPubKey!.PubKey, recovered);

		// Legacy website-style verification (requires P2PKH)
		var p2pkh = hdPubKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, keyManager.GetNetwork());
		Assert.True(VerifyLegacyCompact(p2pkh, message, expectedBase64));
	}

	private static bool VerifyLegacyCompact(BitcoinAddress p2pkh, string message, string base64)
	{
		// Must be a P2PKH address for legacy compact signatures
		if (!p2pkh.ScriptPubKey.IsScriptType(ScriptType.P2PKH))
		{
			return false;
		}

		byte[] bytes;
		try { bytes = Convert.FromBase64String(base64); }
		catch { return false; }

		if (bytes.Length != 65)
		{
			return false;
		}

		// Legacy message hash = doubleSHA256(varstr("Bitcoin Signed Message:\n") || varstr(message))
		var hash = BIP322Signature.CreateMessageHash(message, legacy: true);

		// Parse header & compact r||s (header may include the +8 quirk; we only use the low 2 bits)
		int header = bytes[0];
		int recId = (header - 27) & 3;
		var compact = new CompactSignature(recId, bytes.AsSpan(1, 64).ToArray());

		// Recover pubkey and compare to the P2PKH address
		var recovered = PubKey.RecoverCompact(hash, compact);
		var recoveredAddr = recovered.GetAddress(ScriptPubKeyType.Legacy, p2pkh.Network);
		return recoveredAddr == p2pkh;
	}

	[Theory]
	[InlineData(0, "helloworld")]
	[InlineData(1, "verify taproot bip322")]
	public void SignMessage_BIP322_Taproot_Simple_Verifies(int index, string message)
	{
		var keyManager = KeyManager.Recover(
			new Mnemonic("faculty plunge pilot slice amount salute artist raw amateur excite dial canyon"),
			"",
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.TaprootBIP86));

		var net = keyManager.GetNetwork();
		var destinationProvider = new InternalDestinationProvider(keyManager);

		// Ask for (index+1) external destinations with Taproot preferred, then take the last (our target index).
		var dests = destinationProvider.GetNextDestinations(index + 1, preferTaproot: true).ToArray();
		Assert.NotEmpty(dests);
		var dest = dests.Last();

		// Resolve HdPubKey for the selected destination.
		Assert.True(keyManager.TryGetKeyForScriptPubKey(dest.ScriptPubKey, out HdPubKey? hdPubKey));
		Assert.NotNull(hdPubKey);

		// Assert we actually got a Taproot key (by derivation path type).
		var scriptType = hdPubKey!.FullKeyPath.GetScriptTypeFromKeyPath();
		Assert.Equal(ScriptPubKeyType.TaprootBIP86, scriptType);

		// Optional: double-check via the address script type as well.
		var addr = hdPubKey.GetAddress(net);
		Assert.True(addr.ScriptPubKey.IsScriptType(ScriptType.Taproot), "Expected Taproot (P2TR) address.");

		// Taproot uses BIP-322 Simple proof.
		var sig322 = KeyManagerExtension.SignMessage(keyManager, "", message, hdPubKey, legacy: false);

		var parsed = BIP322Signature.Parse(sig322, net);
		Assert.True(addr.VerifyBIP322(message, parsed));

		// Negative check: flip a byte → verification must fail.
		var tampered = Convert.FromBase64String(sig322);
		tampered[8] ^= 0x01;
		Assert.False(addr.VerifyBIP322(message, BIP322Signature.Parse(Convert.ToBase64String(tampered), net)));
	}

	// --- Optional: BIP-322 verification for both indices (no fixed base64 needed) ---
	[Theory]
	[InlineData(0, "helloworld")]
	[InlineData(1, "helloworld")]
	public void SignMessage_BIP322_Verifies(int index, string message)
	{
		var keyManager = KeyManager.Recover(
			new Mnemonic("faculty plunge pilot slice amount salute artist raw amateur excite dial canyon"),
			"",
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit));

		var destinationProvider = new InternalDestinationProvider(keyManager);
		var net = keyManager.GetNetwork();

		var dests = destinationProvider.GetNextDestinations(index + 1, false).ToArray();
		var dest = dests.Last();
		Assert.True(keyManager.TryGetKeyForScriptPubKey(dest.ScriptPubKey, out HdPubKey? hdPubKey));
		Assert.NotNull(hdPubKey);

		// Use your helper directly to generate BIP-322 (legacy:false)
		var sig322 = KeyManagerExtension.SignMessage(keyManager, "", message, hdPubKey!, legacy: false);
		var addr = hdPubKey!.GetAddress(net);
		var parsed = BIP322Signature.Parse(sig322, net);
		Assert.True(addr.VerifyBIP322(message, parsed));

		// Negative: tamper a byte -> must fail
		var tampered = Convert.FromBase64String(sig322);
		tampered[8] ^= 0x01;
		Assert.False(addr.VerifyBIP322(message, BIP322Signature.Parse(Convert.ToBase64String(tampered), net)));
	}
}
