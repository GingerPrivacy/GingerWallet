using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

public static class Decomposer
{
	public static List<(long Sum, int Count, UInt128 Decomposition)> Decompose(long target, long tolerance, int maxCount, long[] stdDenoms)
	{
		if (maxCount is <= 1 or > 16)
		{
			throw new ArgumentOutOfRangeException(nameof(maxCount), "The maximum decomposition length cannot be greater than 16 or smaller than 2.");
		}
		if (target <= 0)
		{
			throw new ArgumentException("Only positive numbers can be decomposed.", nameof(target));
		}

		if (stdDenoms.Length > 255)
		{
			throw new ArgumentException("Too many denominations. Maximum number is 255.", nameof(target));
		}

		List<(long Sum, int Count, UInt128 Decomposition)> results = new();
		TakeNext(results, tolerance, stdDenoms, target, maxCount, 0, 0, 0, 0);

		return results;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static void TakeNext(List<(long Sum, int Count, UInt128 Decomposition)> results, long tolerance, long[] denoms, long remainingTarget, int remainingCount, int checkIdx, long sum, int count, UInt128 decomposition)
	{
		for (; checkIdx < denoms.Length && denoms[checkIdx] > remainingTarget; checkIdx++) { }
		if (checkIdx >= denoms.Length)
		{
			return;
		}

		var denom = denoms[checkIdx];
		if (remainingCount * denom + tolerance < remainingTarget)
		{
			return;
		}
		TakeNext(results, tolerance, denoms, remainingTarget, remainingCount, checkIdx + 1, sum, count, decomposition);

		decomposition = (decomposition << 8) | (ulong)checkIdx & 0xff;
		sum += denom;
		count++;
		remainingTarget -= denom;
		remainingCount--;

		if (remainingTarget < tolerance)
		{
			results.Add((sum, count, decomposition));
			return;
		}
		if (remainingCount > 0)
		{
			TakeNext(results, tolerance, denoms, remainingTarget, remainingCount, checkIdx, sum, count, decomposition);
		}
	}

	public static IEnumerable<long> ToRealValuesArray(UInt128 decomposition, int count, long[] denoms)
	{
		var list = new long[count];
		for (var i = 0; i < count; i++)
		{
			var index = (int)(decomposition & 0xff);
			list[count - i - 1] = denoms[index];
			decomposition >>= 8;
		}
		return list;
	}
}
