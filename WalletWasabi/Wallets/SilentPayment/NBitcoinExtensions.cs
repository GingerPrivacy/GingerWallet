using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NBitcoin.Crypto;

namespace WalletWasabi.Wallets.SilentPayment;

public static class NBitcoinExtensions
{
	public static SilentPaymentBech32Encoder GetSilentPaymentBech32Encoder(this Network network) =>
		new (Encoders.ASCII.DecodeData(GetHrpForNetwork(network)));

	private static string GetHrpForNetwork(Network network)
	{
		if (network == Network.Main)
		{
			return "sp";
		}
		if (network == Network.TestNet)
		{
			return "tsp";
		}
		if (network == Bitcoin.Instance.Signet)
		{
			return "tsp";
		}
		if (network == Network.RegTest)
		{
			return "tprt";
		}

		throw new ArgumentException($"Network {network.Name} is not supported");
	}

	public static Key Tweak(this Key key)
	{
		using var eckey = ECPrivKey.Create(key.ToBytes());

		// Negate the key if the public key's y-coordinate is odd
		using var workingKey = ECPrivKey.Create((eckey.CreatePubKey().Q.y.IsOdd ? eckey.sec.Negate() : eckey.sec).ToBytes());

		// Compute taproot tweak
		var tag = Hashes.SHA256("TapTweak"u8.ToArray());
		var tweakHash = Hashes.SHA256(WalletWasabi.Helpers.ByteHelpers.Combine(tag, tag, key.PubKey.TaprootInternalKey.ToBytes()));

		// Apply tweak and return new key
		using var tweakedKey = workingKey.TweakAdd(tweakHash);
		return new Key(tweakedKey.sec.ToBytes());
	}
}
