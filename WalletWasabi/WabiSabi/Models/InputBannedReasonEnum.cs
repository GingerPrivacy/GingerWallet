using WalletWasabi.Models;

namespace WalletWasabi.WabiSabi.Models;

public enum InputBannedReasonEnum
{
	[FriendlyName(isLocalized: true)]
	Unknown,

	[FriendlyName(isLocalized: true)]
	BackendStabilitySafety,

	[FriendlyName(isLocalized: true)]
	FailedToVerify,

	[FriendlyName(isLocalized: true)]
	Cheating,

	[FriendlyName(isLocalized: true)]
	Inherited,

	[FriendlyName(isLocalized: true)]
	RoundDisruptionMethodDidNotConfirm,

	[FriendlyName(isLocalized: true)]
	RoundDisruptionMethodDidNotSign,

	[FriendlyName(isLocalized: true)]
	RoundDisruptionMethodDoubleSpent,

	[FriendlyName(isLocalized: true)]
	RoundDisruptionMethodDidNotSignalReadyToSign,

	[FriendlyName(isLocalized: true)]
	LocalCoinVerifier
}
