using GingerCommon.Crypto.Random;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Extensions;

public static class WasabiRandomExtensions
{
	public static int GetInt(this WasabiRandom random, int fromInclusive, int toExclusive)
	{
		if (random is GingerRandomBridge bridge)
		{
			return bridge.GetInt(fromInclusive, toExclusive);
		}

		ArgumentOutOfRangeException.ThrowIfGreaterThan(fromInclusive, toExclusive);
		return new RandomAdapter(random).GetInt(fromInclusive, toExclusive);
	}

	public static string GetString(this WasabiRandom random, int length, string chars)
	{
		Guard.MinimumAndNotNull(nameof(length), length, 1);
		Guard.NotNullOrEmpty(nameof(chars), chars);

		return new string(Enumerable
			.Repeat(chars, length)
			.Select(s => s[random.GetInt(0, s.Length)])
			.ToArray());
	}

	private sealed class RandomAdapter(WasabiRandom random) : GingerRandom
	{
		public override void GetBytes(Span<byte> output) => random.GetBytes(output);
	}
}
