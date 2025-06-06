using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.CoinList.ViewModels;

[NavigationMetaData(
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	public SelectCoinsDialogViewModel(WalletModel wallet, IList<CoinModel> selectedCoins, SendFlowModel sendFlow)
	{
		Title = Resources.CoinControl;

		var transactionInfo = sendFlow.TransactionInfo ?? throw new InvalidOperationException($"Missing required TransactionInfo.");

		CoinList = new CoinListViewModel(sendFlow.CoinList, selectedCoins, allowCoinjoiningCoinSelection: true, ignorePrivacyMode: true, allowSelection: true);

		EnoughSelected = CoinList.Selection.ToObservableChangeSet()
			.ToCollection()
			.Select(coinSelection => wallet.Coins.AreEnoughToCreateTransaction(transactionInfo, coinSelection));

		EnableBack = true;
		NextCommand = ReactiveCommand.Create(OnNext, EnoughSelected);

		SetupCancel(false, true, false);
	}

	public CoinListViewModel CoinList { get; }

	public IObservable<bool> EnoughSelected { get; }

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			CoinList.Dispose();
		}

		base.OnNavigatedFrom(isInHistory);
	}

	private void OnNext()
	{
		Close(DialogResultKind.Normal, CoinList.Selection.GetSmartCoins().ToList());
	}
}
