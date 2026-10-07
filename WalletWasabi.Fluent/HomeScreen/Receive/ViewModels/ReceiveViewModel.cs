using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData.Aggregation;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.Labels.Models;
using WalletWasabi.Fluent.HomeScreen.Labels.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

[NavigationMetaData(
	IconName = "wallet_action_receive",
	Order = 6,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false,
	IsLocalized = true)]
public partial class ReceiveViewModel : RoutableViewModel, IDisposable
{
	private readonly WalletModel _wallet;
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string _defaultReceiveKey;

	public ReceiveViewModel(WalletModel wallet)
	{
		_wallet = wallet;
		_defaultReceiveKey = wallet.Settings.DefaultReceiveScriptType == ScriptPubKeyType.TaprootBIP86 ? "Taproot" : "SegWit";
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		SuggestionLabels = new SuggestionLabelsViewModel(wallet, Intent.Receive, 3);

		var nextCommandCanExecute =
			SuggestionLabels
				.WhenAnyValue(x => x.Labels.Count).ToSignal()
				.Merge(SuggestionLabels.WhenAnyValue(x => x.IsCurrentTextValid).ToSignal())
				.Select(_ => SuggestionLabels.Labels.Count > 0 || SuggestionLabels.IsCurrentTextValid);

		NextCommand = ReactiveCommand.Create(() => OnNext(wallet.Settings.DefaultReceiveScriptType), nextCommandCanExecute);
		NextWithSegwitCommand = ReactiveCommand.Create(() => OnNext(ScriptPubKeyType.Segwit), nextCommandCanExecute);
		NextWithTaprootCommand = ReactiveCommand.Create(() => OnNext(ScriptPubKeyType.TaprootBIP86), nextCommandCanExecute);

		ShowExistingAddressesCommand = ReactiveCommand.Create(OnShowExistingAddresses);
		SetDefaultReceiveCommand = ReactiveCommand.Create<string>(key =>
		{
			if (key is not ("SegWit" or "Taproot") || (key == "Taproot" && !IsTaprootSupported))
			{
				return;
			}

			wallet.Settings.DefaultReceiveScriptType = key == "Taproot" ? ScriptPubKeyType.TaprootBIP86 : ScriptPubKeyType.Segwit;
			wallet.Settings.Save();
			DefaultReceiveKey = key;
		});

		AddressesModel = wallet.Addresses;
	}

	public AddressesModel AddressesModel { get; }

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	public ICommand ShowExistingAddressesCommand { get; }

	public ICommand NextWithTaprootCommand { get; }

	public ICommand NextWithSegwitCommand { get; }

	public ICommand SetDefaultReceiveCommand { get; }

	public IObservable<bool> HasUnusedAddresses => _wallet.Addresses.Unused.ToObservableChangeSet().Count().Select(i => i > 0);

	public bool IsTaprootSupported => _wallet.IsTaprootSupported;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);
		SuggestionLabels.Activate(disposables);
	}

	private void OnNext(ScriptPubKeyType type)
	{
		SuggestionLabels.ForceAdd = true;
		var address = _wallet.Addresses.NextReceiveAddress(SuggestionLabels.Labels, type);
		SuggestionLabels.Labels.Clear();

		UiContext.Navigate().To().ReceiveAddress(_wallet, address, Services.UiConfig.Autocopy);
	}

	private void OnShowExistingAddresses()
	{
		UiContext.Navigate(NavigationTarget.DialogScreen).To().ReceiveAddresses(_wallet);
	}

	public void Dispose() => _disposables.Dispose();
}
