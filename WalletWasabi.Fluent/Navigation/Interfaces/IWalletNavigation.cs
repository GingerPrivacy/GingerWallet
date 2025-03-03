using WalletWasabi.Fluent.HomeScreen.Wallets.Interfaces;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Navigation.Interfaces;

public interface IWalletNavigation
{
	IWalletViewModel? To(IWalletModel wallet);
}

public interface IWalletSelector : IWalletNavigation
{
	IWalletViewModel? SelectedWallet { get; }

	IWalletModel? SelectedWalletModel { get; }
}
