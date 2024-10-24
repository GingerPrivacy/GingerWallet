using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class EconomicCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => Resources.EconomicCoinJoinProfileTitle;

	public override string Description => Resources.EconomicCoinJoinProfileDescription;

	public override int FeeRateMedianTimeFrameHours => 168; // One week median.

	public override CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.CostMinimizing;
}
