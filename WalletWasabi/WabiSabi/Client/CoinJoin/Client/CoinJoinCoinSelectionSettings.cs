namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinJoinCoinSelectionSettings
{
	public CoinJoinCoinSelectionSettings()
	{
	}

	// Use the new, experimental coin selector
	// The old coin selector will still propose a coin list, if it's better or the new one fails to give a candidate, the client will use that
	public bool UseExperimentalCoinSelector { get; set; } = false;

	// The coin selector will check and propose only candidates that has at least one low privacy coin
	public bool ForceUsingLowPrivacyCoins { get; set; } = false;

	// Lowering this value will result to favor coin selections with less weighted anonymity loss (weighted privacy difference between coins)
	public double WeightedAnonymityLossNormal { get; set; } = 3.0;

	// Lowering this value will result to favor coin selections with less expected money rate loss
	public double ValueLossRateNormal { get; set; } = 0.005;

	// The target of coin count in each "bucket" (10k.., 20k.., 40k satoshi.., etc. buckets)
	public double TargetCoinCountPerBucket { get; set; } = 10.0;

	// Call the old coin selector and use the better from the two results
	public bool UseOldCoinSelectorAsFallback { get; set; } = true;
}
