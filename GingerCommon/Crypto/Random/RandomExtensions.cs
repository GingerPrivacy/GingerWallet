using System.Collections.Generic;

namespace GingerCommon.Crypto.Random;

public static class RandomExtensions
{
	public static IList<T> Shuffle<T>(this IList<T> list, GingerRandom random)
	{
		int n = list.Count;
		while (n > 1)
		{
			int k = random.GetInt(0, n);
			n--;
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
		return list;
	}
}
