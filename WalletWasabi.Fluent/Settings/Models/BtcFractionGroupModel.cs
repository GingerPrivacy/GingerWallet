using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Settings.Models;

public record BtcFractionGroupModel(string Name, int[] GroupSizes)
{
	public string ExampleString => Money.Coins(0.12345678m).ToFormattedString(fractionGrouping: GroupSizes);

	public virtual bool Equals(BtcFractionGroupModel? other)
	{
		if (other is null)
		{
			return false;
		}

		return Name == other.Name && GroupSizes.SequenceEqual(other.GroupSizes);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Name, GroupSizes.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)));
	}
};
