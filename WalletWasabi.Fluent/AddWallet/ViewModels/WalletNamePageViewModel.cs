using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class WalletNamePageViewModel : RoutableViewModel
{
	private readonly WalletCreationOptions _options;
	[AutoNotify] private string _walletName;

	public WalletNamePageViewModel(WalletCreationOptions options)
	{
		Title = Resources.WalletName;

		_options = options;
		_walletName = UiContext.WalletRepository.GetNextWalletName();

		EnableBack = true;

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.WalletName)
				.Select(_ => !Validations.Any);

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		this.ValidateProperty(x => x.WalletName, ValidateWalletName);
	}

	private async Task OnNextAsync()
	{
		IsBusy = true;

		// Makes sure we can create a wallet with this wallet name.
		await Task.Run(() => WalletGenerator.GetWalletFilePath(WalletName, Services.WalletManager.WalletDirectories.WalletsDir));

		IsBusy = false;

		var options = _options with { WalletName = WalletName };

		switch (options)
		{
			case WalletCreationOptions.AddNewWallet add:
				UiContext.Navigate().To().RecoveryWords(add);
				break;

			case WalletCreationOptions.ConnectToHardwareWallet chw:
				UiContext.Navigate().To().ConnectHardwareWallet(chw);
				break;

			case WalletCreationOptions.RecoverWallet rec:
				UiContext.Navigate().To().RecoverWallet(rec);
				break;

			case WalletCreationOptions.ImportWallet imp:
				await ImportWalletAsync(imp);
				break;

			default:
				throw new InvalidOperationException($"{nameof(WalletCreationOptions)} not supported: {options?.GetType().Name}");
		}
	}

	private async Task ImportWalletAsync(WalletCreationOptions.ImportWallet options)
	{
		try
		{
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);
			UiContext.Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			await ShowErrorAsync(Resources.ImportWallet, ex.ToUserFriendlyString(), Resources.WalletImportFailed);
			BackCommand.Execute(null);
		}
	}

	private void ValidateWalletName(IValidationErrors errors)
	{
		var error = UiContext.WalletRepository.ValidateWalletName(WalletName);
		if (error is { } e)
		{
			errors.Add(e.Severity, e.Message);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (isInHistory && !UiContext.WalletRepository.HasWallet)
		{
			UiContext.Navigate(CurrentTarget).Back();
		}
		else if (!UiContext.WalletRepository.HasWallet && NextCommand is { } cmd && cmd.CanExecute(default))
		{
			cmd.Execute(default);
		}

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}
}
