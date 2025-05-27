using System.Collections.Generic;

namespace WalletWasabi.Wallets.FilterProcessor;

public record Priority(uint BlockHeight = 0)
{
	public static readonly Comparer<Priority> Comparer = Comparer<Priority>.Create(
		(x, y) =>
		{
			return x.BlockHeight.CompareTo(y.BlockHeight);
		});
}
