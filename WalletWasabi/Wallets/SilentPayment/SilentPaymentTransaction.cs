using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Wallets.SilentPayment;

public static class SilentPaymentTransaction
{
	// BIP174 proprietary global key: prefix "ginger", subtype 1. Signers preserve unknown fields.
	private static readonly byte[] PsbtKey = [0xfc, 6, (byte)'g', (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', 1];
	public static bool HasSilentPaymentOutputs(PSBT psbt) =>
		psbt.Unknown.TryGetValue(PsbtKey, out var value) && value.Length == 1 && value[0] == 1;

	/// <summary>Resolves every estimation placeholder, also for unsigned previews and PSBT exports.</summary>
	public static PSBT Resolve(PSBT psbt, SilentPaymentAddress[] addresses, SmartCoin[] inputs, KeyManager keyManager, string password, Network network)
	{
		var secrets = keyManager.GetSecrets(password, inputs.Select(c => c.ScriptPubKey).ToArray()).ToArray();
		var keys = secrets.Select(s => s.PrivateKey).ToArray();
		var tweakedKeys = new List<Key>();
		try
		{
			var utxos = inputs.Select(coin =>
			{
				foreach (var key in keys)
				{
					if (key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit) == coin.ScriptPubKey)
					{
						return new Utxo(coin.Outpoint, key, coin.ScriptPubKey);
					}
					if (key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86) == coin.ScriptPubKey)
					{
						var tweaked = key.Tweak();
						tweakedKeys.Add(tweaked);
						return new Utxo(coin.Outpoint, tweaked, coin.ScriptPubKey);
					}
				}
				throw new InvalidOperationException("Silent payments require owned SegWit or Taproot key-path inputs.");
			}).ToArray();
			var scripts = SilentPayment.GetPubKeys(addresses, utxos)
				.ToDictionary(p => p.Key.ScriptPubKey, p => new TaprootPubKey(p.Value.Single().ToBytes()).ScriptPubKey);
			var tx = psbt.GetGlobalTransaction();
			var replaced = new HashSet<Script>();
			foreach (var output in tx.Outputs)
			{
				if (scripts.TryGetValue(output.ScriptPubKey, out var script))
				{
					replaced.Add(output.ScriptPubKey);
					output.ScriptPubKey = script;
				}
			}
			if (replaced.Count != scripts.Count)
			{
				throw new InvalidOperationException("A silent payment output is missing from the selected transaction.");
			}
			var resolved = tx.CreatePSBT(network);
			foreach (var (target, source) in resolved.Inputs.Zip(psbt.Inputs)) { target.UpdateFrom(source); }
			resolved.Unknown[PsbtKey] = [1];
			return resolved;
		}
		finally
		{
			foreach (var key in tweakedKeys) { key.Dispose(); }
			foreach (var key in keys) { key.Dispose(); }
		}
	}
}
