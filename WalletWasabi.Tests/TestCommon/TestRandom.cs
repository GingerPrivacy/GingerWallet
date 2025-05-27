using GingerCommon.Crypto.Random;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tests.TestCommon;

public static class TestRandom
{
	public static GingerRandom Get(ulong seed = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		if (seed <= 10)
		{
			// Deterministic seed
			var localFilePath = callerFilePath[callerFilePath.LastIndexOf("WalletWasabi")..].Replace("\\", "/");
			var str = string.Join(",", localFilePath, callerMemberName, seed.ToString(CultureInfo.InvariantCulture));
			return new DeterministicRandom(SHA256.HashData(Encoding.UTF8.GetBytes(str)));
		}
		return new DeterministicRandom(seed);
	}

	public static WasabiRandom Wasabi(ulong seed = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		return new GingerRandomBridge(Get(seed, callerFilePath, callerMemberName));
	}
}
