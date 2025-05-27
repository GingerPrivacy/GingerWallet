using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 2,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true,
	Searchable = false)]
public partial class WalletSettingsViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private string _walletName;
	[AutoNotify] private int _selectedTab;

	public WalletSettingsViewModel(WalletModel walletModel)
	{
		_wallet = walletModel;
		_walletName = walletModel.Name;
		_preferPsbtWorkflow = walletModel.Settings.PreferPsbtWorkflow;
		_selectedTab = 0;
		IsHardwareWallet = walletModel.IsHardwareWallet;
		IsWatchOnly = walletModel.IsWatchOnlyWallet;

		WalletCoinJoinSettings = new WalletCoinJoinSettingsViewModel(walletModel);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().WalletVerifyRecoveryWords(walletModel));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				walletModel.Settings.PreferPsbtWorkflow = value;
				walletModel.Settings.Save();
			});

		this.WhenAnyValue(x => x._wallet.Name).BindTo(this, x => x.WalletName);

		RenameCommand = ReactiveCommand.CreateFromTask(OnRenameWalletAsync);
		ResyncWalletCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var doResync = await UiContext.Navigate().To().ResyncWallet().GetResultAsync();
			if (doResync)
			{
				walletModel.Settings.ResetHeight();
				UiContext.Navigate(MetaData.NavigationTarget).Clear();
				AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true);
			}
		});

		DeleteWalletCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var confirmed = await UiContext.Navigate().To().DeleteWallet(walletModel.Name).GetResultAsync();
			if (confirmed)
			{
				var success = UiContext.WalletRepository.RemoveWallet(walletModel);
				if (!success)
				{
					await ShowErrorAsync(Resources.DeleteWallet, Resources.CouldntDeleteWalletCheckLogs, "");
					return;
				}

				UiContext.Navigate().To().Success();
			}
		});
	}

	public ICommand RenameCommand { get; set; }

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public WalletCoinJoinSettingsViewModel WalletCoinJoinSettings { get; private set; }

	public ICommand VerifyRecoveryWordsCommand { get; }

	public ICommand ResyncWalletCommand { get; }

	public ICommand DeleteWalletCommand { get; }

	private async Task OnRenameWalletAsync()
	{
		await UiContext.Navigate().To().WalletRename(_wallet).GetResultAsync();
		UiContext.WalletRepository.StoreLastSelectedWallet(_wallet);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		WalletCoinJoinSettings.ManuallyUpdateOutputWalletList();
	}
}
