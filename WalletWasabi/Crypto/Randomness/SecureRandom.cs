using GingerSecureRandom = GingerCommon.Crypto.Random.SecureRandom;

namespace WalletWasabi.Crypto.Randomness;

public static class SecureRandom
{
	public static readonly GingerRandomBridge Instance = new(GingerSecureRandom.Instance);
}
