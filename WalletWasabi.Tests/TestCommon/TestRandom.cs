using GingerCommon.Crypto.Random;
using System.Runtime.CompilerServices;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tests.TestCommon;

public static class TestRandom
{
	public static GingerRandom Get(ulong seed = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		if (seed <= 10)
		{
			seed ^= (ulong)callerFilePath.GetHashCode() << 16;
			seed ^= (ulong)callerMemberName.GetHashCode();
		}
		return new DeterministicRandom(seed);
	}

	public static WasabiRandom Wasabi(ulong seed = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		return new GingerRandomBridge(Get(seed, callerFilePath, callerMemberName));
	}
}
