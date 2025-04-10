namespace WalletWasabi.Models;

public enum BtcFractionGroupSize
{
	[FriendlyName(isLocalized: true)]
	[GroupSizes([8])]
	None,

	[FriendlyName(friendlyName:"4-4")]
	[GroupSizes([4,4])]
	FourFour,

	[FriendlyName(friendlyName:"2-3-3")]
	[GroupSizes([2,3,3])]
	TwoThreeThree,
}
