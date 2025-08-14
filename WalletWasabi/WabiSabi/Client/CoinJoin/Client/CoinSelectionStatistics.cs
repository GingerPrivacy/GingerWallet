using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

/// <summary>
/// The main idea of the CoinSelectionStatistics class that we collect the most important properties of a coin selection for a coinjoin and later generate a single score from it.
/// The score calculation logic is in the comparer as the weights we use for the calculation is not (and must not be) part of the CoinSelectionStatistics class.
/// </summary>
public class CoinSelectionStatistics
{
	public virtual Money Amount => Money.Zero;
	public virtual int CoinCount => 0;
	public virtual int TransactionCount => 0;

	public double AnonymityLoss { get; set; } = 0;
	public double ValueLossRate { get; set; } = 0;
	public double BucketScore { get; set; } = 0;

	public double Score { get; internal set; } = double.NaN;

	public override string ToString()
	{
		return $"{Score,6:F3}, ({Amount,10:F8}, {CoinCount,2}, {TransactionCount,2}), ({AnonymityLoss,5:F2}, {ValueLossRate,6:F4}, {BucketScore,5:F2})";
	}
}

/// <summary>
/// The class that is able to order the different CoinSelectionStatistics objects.
/// The weights can be set via the normalization factor of the different properties.
/// </summary>
public class CoinSelectionStatisticsComparer : IComparer<CoinSelectionStatistics>
{
	public CoinSelectionStatisticsComparer(double coinCountNormal, double weightedAnonymityLossNormal, double valueLossRateNormal)
	{
		CoinCountMultiplier = 1.0 / coinCountNormal;
		WeightedAnonymityLossMultiplier = 1.0 / weightedAnonymityLossNormal;
		ValueLossRateMultiplier = 1.0 / valueLossRateNormal;
	}

	public double CoinCountMultiplier { get; }
	public double WeightedAnonymityLossMultiplier { get; }
	public double ValueLossRateMultiplier { get; }

	public double GetScore(CoinSelectionStatistics? coinSelectionStatistics)
	{
		// The smaller is the better
		if (coinSelectionStatistics is null)
		{
			return double.PositiveInfinity;
		}

		// Heavy penalty for single coin selection
		double coinCountScore = coinSelectionStatistics.CoinCount > 1 ? -0.1 * CoinCountMultiplier * coinSelectionStatistics.CoinCount : 0;
		// The goal here (sqrt) that an extra transaction should give deminishing effects while the first ones are vital
		double transactionCountScore = -0.005 * CoinCountMultiplier * (Math.Sqrt(coinSelectionStatistics.TransactionCount) - 1);
		// Already a score
		double bucketScore = 0.01 * coinSelectionStatistics.BucketScore;

		double lossScore = GetLossScore(coinSelectionStatistics);
		double score = coinCountScore + transactionCountScore + lossScore + bucketScore;

		// Don't try to use it as a cache as the previous Score might be calculated with a different Comparer with different multipliers
		coinSelectionStatistics.Score = score;

		return score;
	}

	// Always positive
	public double GetLossScore(CoinSelectionStatistics coinSelectionStatistics)
	{
		double anonymityLossScore = 0.3 * WeightedAnonymityLossMultiplier * (coinSelectionStatistics.CoinCount != 1 ? coinSelectionStatistics.AnonymityLoss : 2.0);
		double valueLossRateScore = 0.3 * ValueLossRateMultiplier * coinSelectionStatistics.ValueLossRate;
		return anonymityLossScore + valueLossRateScore;
	}

	public int Compare(CoinSelectionStatistics? x, CoinSelectionStatistics? y)
	{
		double scoreX = GetScore(x);
		double scoreY = GetScore(y);
		return scoreX.CompareTo(scoreY);
	}
}
