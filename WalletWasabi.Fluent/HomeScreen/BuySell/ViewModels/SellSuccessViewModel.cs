using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SellSuccessViewModel : RoutableViewModel
{
	public SellSuccessViewModel(UiContext uiContext, IWalletModel walletModel, string providerLabel)
	{
		UiContext = uiContext;
		Provider = providerLabel;
		Title = Resources.SellBitcoin;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(() => Navigate().To().Send(walletModel, new SendFlowModel(walletModel.Wallet, walletModel), new LabelsArray(providerLabel), true));
		SendManualControlCommand = ReactiveCommand.Create(() => Navigate().To().ManualControlDialog(walletModel, walletModel.Wallet, new LabelsArray(providerLabel), true));
	}

	public ICommand SendManualControlCommand { get; }
	public string Provider { get; }
}
