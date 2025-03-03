using GingerCommon.Crypto.Random;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

public class GingerRandomBridge : WasabiRandom
{
	private GingerRandom _random;

	public GingerRandomBridge(GingerRandom random)
	{
		_random = random;
	}

	public override void GetBytes(byte[] output)
	{
		_random.GetBytes(output);
	}

	public override void GetBytes(Span<byte> output)
	{
		_random.GetBytes(output);
	}

	public override int GetInt(int fromInclusive, int toExclusive)
	{
		return _random.GetInt(fromInclusive, toExclusive);
	}
}
