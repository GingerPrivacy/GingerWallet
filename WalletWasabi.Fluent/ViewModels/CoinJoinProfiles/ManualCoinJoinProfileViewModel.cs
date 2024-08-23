using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class ManualCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public ManualCoinJoinProfileViewModel(
		int anonScoreTarget,
		int safeMiningFeeRate,
		int feeRateMedianTimeFrameHours,
		bool redCoinIsolation,
		CoinjoinSkipFactors skipFactors)
	{
		AnonScoreTarget = anonScoreTarget;
		SafeMiningFeeRate = safeMiningFeeRate;
		FeeRateMedianTimeFrameHours = feeRateMedianTimeFrameHours;
		RedCoinIsolation = redCoinIsolation;
		SkipFactors = skipFactors;
	}

	public ManualCoinJoinProfileViewModel(IWalletSettingsModel walletSettings)
		: this(
			  walletSettings.AnonScoreTarget,
			  walletSettings.SafeMiningFeeRate,
			  walletSettings.FeeRateMedianTimeFrameHours,
			  walletSettings.RedCoinIsolation,
			  walletSettings.CoinjoinSkipFactors)
	{
	}

	public override string Title => "Custom";

	public override string Description => "";

	public override int AnonScoreTarget { get; }

	public override int SafeMiningFeeRate { get; }
	public override int FeeRateMedianTimeFrameHours { get; }
	public override bool RedCoinIsolation { get; }
	public override CoinjoinSkipFactors SkipFactors { get; }
}
