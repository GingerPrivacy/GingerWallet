using GingerCommon.Crypto.Random;
using WalletWasabi.Extensions;
using GingerRandomBridge = WalletWasabi.Crypto.Randomness.GingerRandomBridge;
using Xunit;
using WasabiRandom = WabiSabi.Crypto.Randomness.WasabiRandom;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class RandomTests
{
	[Fact]
	public void RandomArrayTests()
	{
		Assert.Equal((0, 0), CheckArray(SecureRandom.Instance, 256 * 10000, 0.004, 0.08));
		Assert.Equal((0, 0), CheckArray(new DeterministicRandom(0), 256 * 10000, 0.002, 0.04));
		Assert.Equal((0, 0), CheckArray(new DeterministicRandom(2), 256 * 10000, 0.002, 0.04));
		Assert.Equal((0, 0), CheckArray(new DeterministicRandom(0x0900be81f6df6068), 256 * 10000, 0.002, 0.04));
	}

	[Fact]
	public void RandomIntTests()
	{
		Assert.Equal(0, CheckInt(SecureRandom.Instance, 895, 1990, [1001, 1301, 1514, 1848, 1920], 10000, 0.1));
		Assert.Equal(0, CheckInt(new DeterministicRandom(0), 895, 1990, [1001, 1301, 1514, 1848, 1920], 10000, 0.05));
		Assert.Equal(0, CheckInt(new DeterministicRandom(2), 895, 1990, [1001, 1301, 1514, 1848, 1920], 10000, 0.05));
		Assert.Equal(0, CheckInt(new DeterministicRandom(0x0900be81f6df6068), 895, 1990, [1001, 1301, 1514, 1848, 1920], 10000, 0.05));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RandomBridgeTests(bool useBridge)
	{
		var ginger = new DeterministicRandom(27);
		WasabiRandom wasabi = useBridge
			? new GingerRandomBridge(new DeterministicRandom(27))
			: new ByteOnlyRandom(new DeterministicRandom(27));

		var gingerArray = new byte[97];
		var wasabiArray = new byte[97];
		ginger.GetBytes(gingerArray);
		wasabi.GetBytes(wasabiArray);
		Assert.Equal(gingerArray, wasabiArray);

		Assert.Equal(ginger.GetInt(1222, 1780), wasabi.GetInt(1222, 1780));
		Assert.Equal(ginger.GetInt(12221222, 17801780), wasabi.GetInt(12221222, 17801780));
		Assert.Equal(ginger.GetInt(int.MinValue, int.MaxValue), wasabi.GetInt(int.MinValue, int.MaxValue));
		Assert.Equal(ginger.GetString(21, "abcdef012345"), wasabi.GetString(21, "abcdef012345"));
		Assert.Throws<ArgumentOutOfRangeException>(() => wasabi.GetString(0, "abc"));
		Assert.Throws<ArgumentException>(() => wasabi.GetString(1, ""));
	}

	private sealed class ByteOnlyRandom(GingerRandom random) : WasabiRandom
	{
		public override void GetBytes(byte[] output) => random.GetBytes(output);

		public override void GetBytes(Span<byte> output) => random.GetBytes(output);
	}

	private (int, int) CheckArray(GingerRandom random, int arrayCount, double bitThreshold, double byteThreshold)
	{
		int[] bits = new int[8];
		int[] bytes = new int[256];
		byte[] randomArray = new byte[arrayCount];

		random.GetBytes(randomArray);

		for (int idx = 0; idx < arrayCount; idx++)
		{
			byte v = randomArray[idx];
			bytes[v]++;
			for (int bitIdx = 0; bitIdx < 8; bitIdx++)
			{
				if ((v & 1) != 0)
				{
					bits[bitIdx]++;
				}
				v >>= 1;
			}
		}
		int resBits = 0;
		double avgBits = arrayCount / 2;
		int minBitsCount = (int)(avgBits * (1 - bitThreshold));
		int maxBitsCount = (int)(avgBits * (1 + bitThreshold));
		for (int idx = 0; idx < bits.Length; idx++)
		{
			int count = bits[idx];
			if (count < minBitsCount || count > maxBitsCount)
			{
				resBits |= 1 << idx;
			}
		}
		int resBytes = 0;
		double avgBytes = arrayCount / (double)bytes.Length;
		int minBytesCount = (int)(avgBytes * (1 - byteThreshold));
		int maxBytesCount = (int)(avgBytes * (1 + byteThreshold));
		for (int idx = 0; idx < bytes.Length; idx++)
		{
			int count = bytes[idx];
			if (count < minBytesCount || count > maxBytesCount)
			{
				resBytes++;
			}
		}

		return (resBits, resBytes);
	}

	private int CheckInt(GingerRandom random, int min, int max, int[] checkValues, int checkCount, double threshold)
	{
		int[] counts = new int[checkValues.Length];

		for (int idx = 0; idx < checkCount; idx++)
		{
			int value = random.GetInt(min, max);
			if (value < min || value >= max)
			{
				return -1;
			}
			for (int checkValueIdx = 0; checkValueIdx < checkValues.Length; checkValueIdx++)
			{
				if (value < checkValues[checkValueIdx])
				{
					counts[checkValueIdx]++;
				}
			}
		}

		int res = 0;
		for (int checkValueIdx = 0; checkValueIdx < checkValues.Length; checkValueIdx++)
		{
			double actual = (double)counts[checkValueIdx] / checkCount;
			double expectedAvg = (double)(checkValues[checkValueIdx] - min) / (max - min);
			if (Math.Abs(actual / expectedAvg - 1) > threshold)
			{
				res |= 1 << checkValueIdx;
			}
		}

		return res;
	}
}
