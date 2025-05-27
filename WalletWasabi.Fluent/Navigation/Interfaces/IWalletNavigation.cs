using WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Navigation.Interfaces;

public interface IWalletNavigation
{
	WalletViewModel? To(WalletModel wallet);
}
