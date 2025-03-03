using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.PrivacyRing.ViewModels;

public class PrivacyBarItemViewModel : ViewModelBase
{
	public PrivacyBarItemViewModel(PrivacyLevel privacyLevel, decimal amount)
	{
		PrivacyLevel = privacyLevel;
		Amount = amount;
	}

	public decimal Amount { get; }

	public PrivacyLevel PrivacyLevel { get; }
}
