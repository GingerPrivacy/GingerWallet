using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinCollectionStatistics
{
	public CoinCollectionStatistics(List<SmartCoin> coins, double targetBucketCoinCount)
	{
		Amount = 0L;
		Buckets = new();
		BucketBalance = new();
		Coins = new(coins);
		TargetBucketCoinCount = targetBucketCoinCount;
		RefreshStatistics();
	}

	public void RefreshStatistics()
	{
		Amount = 0L;
		Buckets.Clear();
		foreach (var coin in Coins)
		{
			if (coin.IsSpent())
			{
				continue;
			}
			Amount += coin.Amount;
			int bucketIndex = CoinJoinCoinSelector.GetBucketIndex(coin);
			var list = Buckets.GetOrCreate(bucketIndex);
			list.Add(coin);
		}
		HighestBucketIndex = Buckets.Keys.MaxOrDefault(0);

		// The zero element containing the dust, we don't use it to calc index 1
		// Also, We generate all buckets under HighestBucketIndex
		BucketBalance.Add(Buckets.GetOrCreate(0).Count);
		BucketScore = 0;
		double res = 0;
		for (int idx = 1; idx <= HighestBucketIndex; idx++)
		{
			res = 0.5 * res + Buckets.GetOrCreate(idx).Count - TargetBucketCoinCount;
			BucketBalance.Add(res);
			BucketScore += Math.Pow(res / TargetBucketCoinCount, 2);
		}
		BucketScore /= HighestBucketIndex;
	}

	public Money GetAmountFromBucketIndex(int bucketIndex)
	{
		return Buckets.Where(x => x.Key >= bucketIndex).Sum(x => x.Value.Sum(x => x.Amount));
	}

	public int GetCoinCountFromBucketIndex(int bucketIndex)
	{
		return Buckets.Where(x => x.Key >= bucketIndex).Sum(x => x.Value.Count);
	}

	public Money Amount { get; private set; }
	public Dictionary<int, List<SmartCoin>> Buckets { get; }
	public List<double> BucketBalance { get; }
	public double BucketScore { get; private set; }
	public List<SmartCoin> Coins { get; }
	public double TargetBucketCoinCount { get; set; }
	public int HighestBucketIndex { get; private set; }
}
