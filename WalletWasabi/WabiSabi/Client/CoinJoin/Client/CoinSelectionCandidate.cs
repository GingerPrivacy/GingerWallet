using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinSelectionCandidate : CoinSelectionStatistics
{
	private class CoinRemovalStatistics : CoinSelectionStatistics
	{
		public CoinRemovalStatistics(SmartCoin coin, int coinCount, int transactionCount, double anonymityLoss, double valueLossRate, double bucketScore)
		{
			Coin = coin;

			_coinCount = coinCount;
			_transactionCount = transactionCount;

			AnonymityLoss = anonymityLoss;
			ValueLossRate = valueLossRate;
			BucketScore = bucketScore;
		}

		public override Money Amount => Coin.Amount;
		public override int CoinCount => _coinCount;
		public override int TransactionCount => _transactionCount;

		public SmartCoin Coin { get; }

		private int _coinCount;
		private int _transactionCount;
	}

	public CoinSelectionCandidate(List<SmartCoin> coins, CoinJoinCoinSelectionParameters coinSelectionParameters)
	{
		Coins = new(coins);
		RemovedCoins = [];
		_coinSelectionParameters = coinSelectionParameters;
		Refresh();
	}

	public override string ToString()
	{
		return $"{base.ToString()}, {MinimumAnonymitySet,5:F2}";
	}

	public bool Equals(CoinSelectionCandidate other)
	{
		if (CoinCount != other.CoinCount || Amount != other.Amount || MinimumAnonymitySet != other.MinimumAnonymitySet)
		{
			return false;
		}

		for (int idx = 0, len = CoinCount; idx < len; idx++)
		{
			if (!other.Coins.Contains(Coins[idx]))
			{
				return false;
			}
		}

		return true;
	}

	// Calculate some of the decision factors
	[MemberNotNull(nameof(_amount))]
	public void Refresh()
	{
		_amount = 0L;
		_vsize = 0;
		_weightedAnonymitySet = 0;
		_transactions.Clear();
		_zeroCoin = Coins.FirstOrDefault();
		_selectionBuckets.Clear();
		MinimumAnonymitySet = double.PositiveInfinity;

		foreach (var coin in Coins)
		{
			_amount += coin.Amount;
			MinimumAnonymitySet = Math.Min(coin.AnonymitySet, MinimumAnonymitySet);
			_weightedAnonymitySet += coin.AnonymitySet * coin.Amount.Satoshi;
			_vsize += coin.ScriptPubKey.EstimateInputVsize();
			_transactions.AddValue(coin.TransactionId, 1);

			int bucketIndex = CoinJoinCoinSelector.GetBucketIndex(coin);
			var list = _selectionBuckets.GetOrCreate(bucketIndex);
			list.Add(coin);
		}

		AnonymityLoss = Amount.Satoshi > 0 ? _weightedAnonymitySet / Amount.Satoshi - MinimumAnonymitySet : double.PositiveInfinity;
		ValueLossRate = (_coinSelectionParameters.MiningFeeRate.GetFee(_vsize).Satoshi + _coinSelectionParameters.CoinJoinLoss) / (double)Amount.Satoshi;
		BucketScore = CalculateBucketScore(-1);

		_coinSelectionParameters.Comparer.GetScore(this);
	}

	private double CalculateBucketScore(int optionalRemovalIndex)
	{
		var walletBuckets = _coinSelectionParameters.WalletStatistics.Buckets;
		var targetBucketCoinCount = _coinSelectionParameters.WalletStatistics.TargetBucketCoinCount;
		var highestBukcetIndex = _coinSelectionParameters.WalletStatistics.HighestBucketIndex;
		double bucketScore = 0;
		double res = 0;
		for (int idx = _coinSelectionParameters.StartingBucketIndex; idx <= highestBukcetIndex; idx++)
		{
			res = 0.5 * res + (walletBuckets.GetValueOrDefault(idx)?.Count ?? 0) - (_selectionBuckets.GetValueOrDefault(idx)?.Count ?? 0) + (idx == optionalRemovalIndex ? 1 : 0) - targetBucketCoinCount;
			bucketScore += res * res;
		}
		bucketScore /= highestBukcetIndex * targetBucketCoinCount * targetBucketCoinCount;

		return bucketScore;
	}

	public bool MeetTheRequirements(int maxCoinNumer, double lowestSensitivity)
	{
		// Goals (both direct and indirect):
		// 1) Meet the AnonymityLoss, ValueLoss and maxCoinNumber requirements
		// 2) The highest local bucket should contain at least 2 elements
		// 3) Don't put to many coins from the wallet buckets that has few elements
		// 4) Has as small AnonymityLoss as possible
		// 5) Have as many transaction's coins as possible

		double sensitivity = 5.0;
		List<CoinRemovalStatistics> removable = new();
		int coinNumber;
		for (; (coinNumber = _selectionBuckets.Values.Sum(x => x.Count)) > 2 && (coinNumber > maxCoinNumer || sensitivity > 1.1);)
		{
			removable.Clear();
			if (CollectRemovableCoins(removable, _selectionBuckets, sensitivity))
			{
				// There was an out of order removal
				continue;
			}
			if ((removable.Count < 3 && sensitivity > 0) || removable.Count == 0)
			{
				sensitivity -= 0.5;
				if (sensitivity < lowestSensitivity)
				{
					break;
				}
				continue;
			}

			removable.Sort(_coinSelectionParameters.Comparer);
			var toRemove = removable.Take(8).RandomElement(_coinSelectionParameters.Random);
			if (toRemove is not null)
			{
				int bucketIndex = CoinJoinCoinSelector.GetBucketIndex(toRemove.Coin);
				var removeBucket = _selectionBuckets.GetOrCreate(bucketIndex);
				RemoveCoinIfPossible(removeBucket, removeBucket.IndexOf(toRemove.Coin));
				if (removeBucket.Count == 0)
				{
					_selectionBuckets.Remove(bucketIndex);
				}
			}
		}

		return GoodCandidate && coinNumber <= maxCoinNumer;
	}

	private bool CollectRemovableCoins(List<CoinRemovalStatistics> removable, Dictionary<int, List<SmartCoin>> localBucket, double sensitivity)
	{
		int highestBucket = localBucket.Keys.MaxOrDefault(0);
		for (int idx = highestBucket; idx > 0; idx--)
		{
			List<SmartCoin>? bucket = localBucket.GetValueOrDefault(idx);
			if (bucket is null || bucket.Count == 0)
			{
				continue;
			}

			if (bucket.Count == 1)
			{
				double balanceBelow = idx > 1 ? _coinSelectionParameters.WalletStatistics.BucketBalance[idx - 1] : 0;
				List<SmartCoin>? bucketAbove = localBucket.GetValueOrDefault(idx + 1);
				if (balanceBelow > 0 && (bucketAbove?.Count ?? 0) == 0)
				{
					// This would most likely lost from this bucket, but we should not let it go
					if (RemoveCoinIfPossible(bucket, 0))
					{
						return true;
					}
				}
				continue;
			}
			double balance = _coinSelectionParameters.WalletStatistics.BucketBalance[idx];
			if (bucket.Count > balance + sensitivity)
			{
				// We should remove a Coin from this list
				var all = bucket.Select(GetCoinRemovalResult).Where(IsRemovable);

				removable.AddRange(all);
			}
		}
		return false;
	}

	private bool RemoveCoinIfPossible(List<SmartCoin> bucket, int coinIdx)
	{
		if (coinIdx >= 0 && coinIdx < bucket.Count)
		{
			var coin = bucket[coinIdx];
			if (!IsCoinRemovable(coin))
			{
				return false;
			}
			RemoveCoin(coin);
			return true;
		}

		return false;
	}

	private void RemoveCoin(SmartCoin coin)
	{
		if (Coins.Remove(coin))
		{
			RemovedCoins.Add(coin);
			Refresh();
		}
	}

	private bool IsCoinRemovable(SmartCoin coin)
	{
		return IsRemovable(GetCoinRemovalResult(coin));
	}

	private bool IsRemovable(CoinRemovalStatistics diff)
	{
		if (_zeroCoin == diff.Coin)
		{
			return false;
		}

		if (diff.ValueLossRate > ValueLossRate && diff.ValueLossRate > _coinSelectionParameters.MaxValueLossRate)
		{
			return false;
		}
		if (diff.AnonymityLoss > AnonymityLoss && diff.AnonymityLoss > _coinSelectionParameters.MaxWeightedAnonymityLoss)
		{
			return false;
		}

		return true;
	}

	private CoinRemovalStatistics GetCoinRemovalResult(SmartCoin coin)
	{
		var newAmount = Amount - coin.Amount;
		var newWeightedAnonymitySet = _weightedAnonymitySet - coin.AnonymitySet * coin.Amount.Satoshi;
		var newVSize = _vsize - coin.ScriptPubKey.EstimateInputVsize();

		var newAnonymityLoss = newWeightedAnonymitySet / newAmount.Satoshi - MinimumAnonymitySet;
		var newValueLossRate = (_coinSelectionParameters.MiningFeeRate.GetFee(newVSize).Satoshi + _coinSelectionParameters.CoinJoinLoss) / (double)newAmount.Satoshi;

		int bucketIndex = CoinJoinCoinSelector.GetBucketIndex(coin);
		double newBucketScore = CalculateBucketScore(bucketIndex);

		return new(coin, CoinCount - 1, TransactionCount - (_transactions.GetValueOrDefault(coin.TransactionId, 0) == 1 ? 1 : 0), newAnonymityLoss, newValueLossRate, newBucketScore);
	}

	public bool GoodCandidate => AnonymityLoss <= _coinSelectionParameters.MaxWeightedAnonymityLoss && ValueLossRate <= _coinSelectionParameters.MaxValueLossRate;

	private Money _amount;
	public override Money Amount => _amount;
	public override int CoinCount => Coins.Count;
	public override int TransactionCount => _transactions.Count;

	public double MinimumAnonymitySet { get; set; }

	public List<SmartCoin> Coins { get; }
	public List<SmartCoin> RemovedCoins { get; }

	private SmartCoin? _zeroCoin;
	private Dictionary<uint256, int> _transactions = new();
	private double _weightedAnonymitySet;
	private int _vsize;
	private Dictionary<int, List<SmartCoin>> _selectionBuckets = new();

	private CoinJoinCoinSelectionParameters _coinSelectionParameters;
}
