using NBitcoin;
using NBitcoin.BIP322;
using System.Linq;
using System.Security;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

public static class KeyManagerExtension
{
	/// <summary>
	/// Controls the first byte ("header") of legacy compact signatures.
	/// Standard=27..34 as per de-facto convention.
	/// Plus8 allows reproducing historical/quirky signers that emitted header+8
	/// (verifiers usually mask off the extra bit, so both parse identically).
	/// </summary>
	public enum LegacyHeaderStyle
	{
		Standard = 0,
		Plus8 = 8
	}

	/// <summary>
	/// Signs an arbitrary message with the HdPubKey's private key.
	///
	/// Two modes:
	///  - legacy=true  → legacy "Bitcoin Signed Message" compact ECDSA (65 bytes, base64).
	///                   Works best with P2PKH verifiers. Header byte is adjustable to reproduce
	///                   historical outputs; r||s is produced by RFC6979 (deterministic k).
	///  - legacy=false → BIP-322 (Simple/Full) proof depending on address type (SegWit/Taproot).
	///                   Safer & standard for bc1... addresses; bytes are library-specific,
	///                   so verify, don't compare for byte-equality.
	///
	/// Notes / gotchas (short and on-point):
	///  - Legacy preimage is double-SHA256(varstr("Bitcoin Signed Message:\n") || varstr(message)).
	///  - Compact legacy signature layout: [header (1)] [r (32)] [s (32)].
	///    header = 27 + recId (0..3) + (IsCompressed ? 4 : 0) + headerStyle (0 or 8).
	///    Many verifiers only use (header-27)&3 and (header-27)&4, so Standard and Plus8 both verify.
	///  - We *recompute* recId by trying 0..3 and checking pubkey recovery. This guarantees the header
	///    decodes back to the exact HdPubKey regardless of library internals.
	///  - forceLowR=false may be needed to reproduce old fixtures; low-R only affects DER/compact encoding
	///    choice of r (same signature validity).
	///  - Legacy verification on websites typically expects a P2PKH address; BIP-322 should be used for
	///    SegWit/Taproot (bc1...) and is what modern wallets accept.
	/// </summary>
	public static string SignMessage(
		this KeyManager keyManager,
		string password,
		string message,
		HdPubKey hdPubKey,
		bool legacy = true,
		LegacyHeaderStyle headerStyle = LegacyHeaderStyle.Standard,
		bool forceLowR = true)
	{
		Guard.NotNullOrEmptyOrWhitespace(message, nameof(message));
		Guard.NotNull(nameof(hdPubKey), hdPubKey);

		// Can't sign without secrets (watch-only / HW-only without software signing)
		if (keyManager.IsWatchOnly)
		{
			throw new SecurityException("Wallet is watch-only/hardware-only; cannot sign.");
		}

		// Must know the derivation to locate the child secret
		if (hdPubKey.FullKeyPath is null)
		{
			throw new InvalidOperationException("HdPubKey does not have a FullKeyPath.");
		}

		var network = keyManager.GetNetwork();
		var spk = hdPubKey.GetAssumedScriptPubKey();

		// Retrieve the child extended key for this script; throws if not found
		var ek = keyManager.GetSecrets(password, spk).SingleOrDefault()
				 ?? throw new InvalidOperationException($"The signing key for '{spk}' was not found.");
		var key = ek.GetBitcoinSecret(network, spk).PrivateKey;

		if (legacy)
		{
			// LEGACY COMPACT (Bitcoin Signed Message)
			// Preimage = varstr("Bitcoin Signed Message:\n") || varstr(message)
			// Hash = double-SHA256(preimage)
			var msgHash = BIP322Signature.CreateMessageHash(message, legacy: true);

			// RFC6979 deterministic ECDSA; low-R toggle for fixture reproduction
			var compact = key.SignCompact(msgHash, forceLowR: forceLowR);

			// Determine recId (0..3) by recovery so our header definitely maps to this pubkey
			int recId = -1;
			for (int i = 0; i < 4; i++)
			{
				var candidate = new CompactSignature(i, compact.Signature);
				if (PubKey.RecoverCompact(msgHash, candidate) == hdPubKey.PubKey)
				{
					recId = i;
					break;
				}
			}
			if (recId < 0)
			{
				throw new InvalidOperationException("Could not determine recovery id.");
			}

			// Header layout:
			//   27 base + recId (0..3) + 4 if compressed + optional +8 for historical compatibility
			bool compressed = hdPubKey.PubKey.IsCompressed; // HD keys are typically compressed
			byte header = (byte)(27 + recId + (compressed ? 4 : 0) + (int)headerStyle);

			// Assemble 65-byte payload: header + r||s (big-endian, fixed 32 bytes each)
			var sig65 = new byte[65];
			sig65[0] = header;
			Buffer.BlockCopy(compact.Signature, 0, sig65, 1, 64);

			// Most sites/APIs take base64 of these 65 bytes
			return Convert.ToBase64String(sig65);
		}
		else
		{
			// BIP-322 (preferred for SegWit/Taproot). Produces either a Simple proof (witness stack)
			// or a Full proof (serialized tx), both accepted by modern verifiers for the address type.
			var addr = hdPubKey.GetAddress(network);

			// Choose proof flavor: Simple for BIP84/86 single-key (P2WPKH/P2TR), Legacy for P2PKH
			var sigType = hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath() switch
			{
				ScriptPubKeyType.Segwit => SignatureType.Simple, // BIP84 P2WPKH
				ScriptPubKeyType.TaprootBIP86 => SignatureType.Simple, // BIP86 P2TR single-key
				_ => SignatureType.Legacy // P2PKH fallback
			};

			// NOTE: BIP-322 serialization can vary by implementation; do not byte-compare between libs.
			// Always verify with addr.VerifyBIP322(message, parsedSignature).
			return key.SignBIP322(addr, message, sigType).ToBase64();
		}
	}
}
