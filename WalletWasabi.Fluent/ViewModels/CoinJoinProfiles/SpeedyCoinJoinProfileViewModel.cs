using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class SpeedyCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => Resources.SpeedyCoinJoinProfileTitle;

	public override string Description => Resources.SpeedyCoinJoinProfileDescription;

	public override int SafeMiningFeeRate => 30;

	public override int FeeRateMedianTimeFrameHours => 0;

	public override CoinjoinSkipFactors SkipFactors { get; } = CoinjoinSkipFactors.SpeedMaximizing;
}
