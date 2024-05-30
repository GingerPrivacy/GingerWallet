using System.IO;
using NBitcoin;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Nostr;

public class NostrKeyManager : IDisposable
{
	private readonly string _keyFileName = "discovery.key";
	private readonly string _folderName = "Nostr";

	public NostrKeyManager(string dataDir)
	{
		using var key = GetOrCreateKey(dataDir);
		if (!Context.Instance.TryCreateECPrivKey(key.ToBytes(), out var ecPrivKey))
		{
			throw new InvalidOperationException("Failed to create ECPrivKey");
		}

		Key = ecPrivKey;
	}

	public ECPrivKey Key { get; }

	private Key GetOrCreateKey(string dataDir)
	{
		var folderPath = Path.Combine(dataDir, _folderName);
		var keyPath = Path.Combine(folderPath, _keyFileName);

		if (!Directory.Exists(folderPath))
		{
			Directory.CreateDirectory(folderPath);
		}

		if (File.Exists(keyPath))
		{
			var keyBytes = File.ReadAllBytes(keyPath);
			return new Key(keyBytes);
		}
		else
		{
			var key = new Key();
			File.WriteAllBytes(keyPath, key.ToBytes());
			return key;
		}
	}

	public void Dispose()
	{
		Key.Dispose();
	}
}
