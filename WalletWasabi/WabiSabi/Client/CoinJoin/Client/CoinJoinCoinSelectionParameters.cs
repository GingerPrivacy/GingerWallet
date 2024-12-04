using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

/// <summary>
/// Collected paramters for the CoinJoinCoinSelector to work with.
/// </summary>
/// <param name="MiningFeeRate">Mining fee rate, the coordinator will use.</param>
/// <param name="MinInputAmount">Minimum input amount in satoshi.</param>
/// <param name="CoinJoinLoss">Fix loss on the coinjoin in satoshi for further calculation.</param>
/// <param name="MaxCoinLossRate">If a coin would loss from its value due to the mining fee then we should not add it at all</param>
/// <param name="MaxValueLossRate">The target maximum value loss on the coinjoin</param>
/// <param name="MaxWeightedAnonymityLoss">The target maximum weighted anonymity loss on the coins</param>
/// <param name="WalletStatistics">Useful statistics from the wallet</param>
/// <param name="Random">The random geneator to be used</param>
public record CoinJoinCoinSelectionParameters(
	FeeRate MiningFeeRate,
	long MinInputAmount,
	long CoinJoinLoss,
	double MaxCoinLossRate,
	double MaxValueLossRate,
	double MaxWeightedAnonymityLoss,
	CoinCollectionStatistics WalletStatistics,
	CoinSelectionStatisticsComparer Comparer,
	WasabiRandom Random)
{
	// This paramter leads to drop every coin
	public static readonly CoinJoinCoinSelectionParameters Empty = new(FeeRate.Zero, 10000, 0, 0, 0, 0, new([], 0), new(10, 3.0, 0.01), SecureRandom.Instance);

	public bool IsCoinAboveAllowedLoss(ISmartCoin coin) => MiningFeeRate.GetFee(coin.ScriptType.EstimateInputVsize()).Satoshi / (double)coin.Amount.Satoshi > MaxCoinLossRate;

	public int StartingBucketIndex { get; init; } = (int)Math.Max(Math.Round(Math.Log2(MinInputAmount / 5000.0) + 1), 0);
}
