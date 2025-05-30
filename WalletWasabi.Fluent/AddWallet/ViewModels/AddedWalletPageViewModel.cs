using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class AddedWalletPageViewModel : RoutableViewModel
{
	private readonly WalletSettingsModel _walletSettings;
	private WalletModel? _wallet;

	public AddedWalletPageViewModel(WalletSettingsModel walletSettings, WalletCreationOptions options)
	{
		Title = Resources.Success;

		_walletSettings = walletSettings;

		WalletName = options.WalletName!;
		WalletType = walletSettings.WalletType;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(() => OnNextAsync(options));
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	private async Task OnNextAsync(WalletCreationOptions options)
	{
		if (_wallet is not { })
		{
			return;
		}

		IsBusy = true;

		await AutoLoginAsync(options);

		IsBusy = false;

		await Task.Delay(UiConstants.CloseSuccessDialogMillisecondsDelay);

		UiContext.Navigate(CurrentTarget).Clear();

		UiContext.Navigate().To(_wallet);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet = UiContext.WalletRepository.SaveWallet(_walletSettings);

		if (NextCommand is not null && NextCommand.CanExecute(default))
		{
			NextCommand.Execute(default);
		}
	}

	private async Task AutoLoginAsync(WalletCreationOptions? options)
	{
		if (_wallet is not { })
		{
			return;
		}

		var password =
			options switch
			{
				WalletCreationOptions.AddNewWallet add => add.Password,
				WalletCreationOptions.RecoverWallet rec => rec.Password,
				WalletCreationOptions.ConnectToHardwareWallet => "",
				_ => null
			};

		if (password is { })
		{
			var termsAndConditionsAccepted = await TermsAndConditionsViewModel.TryShowAsync(UiContext, _wallet);
			if (termsAndConditionsAccepted)
			{
				await _wallet.Auth.LoginAsync(password);
			}
		}
	}
}
