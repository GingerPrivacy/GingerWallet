using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.CoinList.ViewModels;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

[NavigationMetaData(
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ManualControlDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	[AutoNotify] private bool _hasSelection;

	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;
	private readonly LabelsArray? _preLabel;
	private readonly bool _continueWithFixedAmount;

	public ManualControlDialogViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet, LabelsArray? preLabel = null, bool continueWithFixedAmount = false)
	{
		UiContext = uiContext;
		Title = "Manual Control";

		CoinList = new CoinListViewModel(UiContext, walletModel.Coins, [], allowCoinjoiningCoinSelection: true, ignorePrivacyMode: true, allowSelection: true);

		var nextCommandCanExecute =
			CoinList.Selection
					.ToObservableChangeSet()
					.ToCollection()
					.Select(c => c.Count > 0);

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);

		SelectedAmount =
			CoinList.Selection
					.ToObservableChangeSet()
					.ToCollection()
					.Select(c => c.Any() ? walletModel.AmountProvider.Create(c.TotalAmount()) : null);

		ToggleSelectionCommand = ReactiveCommand.Create(() => SelectAll(!CoinList.Selection.Any()));

		SetupCancel(true, true, true);
		_walletModel = walletModel;
		_wallet = wallet;
		_preLabel = preLabel;
		_continueWithFixedAmount = continueWithFixedAmount;
	}

	public CoinListViewModel CoinList { get; }

	public ICommand ToggleSelectionCommand { get; }

	public IObservable<Amount?> SelectedAmount { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		CoinList.CoinItems
				.ToObservableChangeSet()
				.WhenPropertyChanged(x => x.IsSelected)
				.Select(_ => CoinList.Selection.Count > 0)
				.BindTo(this, x => x.HasSelection)
				.DisposeWith(disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			CoinList.Dispose();
		}
	}

	private void OnNext()
	{
		var coins = CoinList.Selection.GetSmartCoins().ToList();

		var sendParameters = new SendFlowModel(_wallet, _walletModel, coins);

		Navigate().To().Send(_walletModel, sendParameters, _preLabel, _continueWithFixedAmount);
	}

	private void SelectAll(bool value)
	{
		foreach (var coin in CoinList.CoinItems)
		{
			coin.IsSelected = value;
		}
	}
}
