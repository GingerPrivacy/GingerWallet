using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.General,
	IconName = "nav_add_circle_24_regular",
	IconNameFocused = "nav_add_circle_24_filled",
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarPosition = NavBarPosition.Bottom,
	NavBarSelectionMode = NavBarSelectionMode.Button,
	IsLocalized = true)]
public partial class AddWalletPageViewModel : DialogViewModelBase<Unit>
{
	public AddWalletPageViewModel()
	{
		CreateWalletCommand = ReactiveCommand.Create(OnCreateWallet);

		ConnectHardwareWalletCommand = ReactiveCommand.Create(OnConnectHardwareWallet);

		ImportWalletCommand = ReactiveCommand.CreateFromTask(OnImportWalletAsync);

		RecoverWalletCommand = ReactiveCommand.Create(OnRecoverWallet);
	}

	public ICommand CreateWalletCommand { get; }

	public ICommand ConnectHardwareWalletCommand { get; }

	public ICommand ImportWalletCommand { get; }

	public ICommand RecoverWalletCommand { get; }

	private void OnCreateWallet()
	{
		var options = new WalletCreationOptions.AddNewWallet().WithNewMnemonic();
		UiContext.Navigate().To().WalletNamePage(options);
	}

	private void OnConnectHardwareWallet()
	{
		UiContext.Navigate().To().WalletNamePage(new WalletCreationOptions.ConnectToHardwareWallet());
	}

	private async Task OnImportWalletAsync()
	{
		try
		{
			var file = await FileDialogHelper.OpenFileAsync(Resources.ImportWallet, new[] { "json" });

			if (file is null)
			{
				return;
			}

			var filePath = file.Path.AbsolutePath;
			var walletName = Path.GetFileNameWithoutExtension(filePath);

			var options = new WalletCreationOptions.ImportWallet(walletName, filePath);

			var validationError = UiContext.WalletRepository.ValidateWalletName(walletName);
			if (validationError is { })
			{
				UiContext.Navigate().To().WalletNamePage(options);
				return;
			}

			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);

			UiContext.Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Resources.ImportWallet, ex.ToUserFriendlyString(), Resources.WalletImportFailed);
		}
	}

	private void OnRecoverWallet()
	{
		UiContext.Navigate().To().WalletNamePage(new WalletCreationOptions.RecoverWallet());
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
	}

	public async Task Activate()
	{
		MainViewModel.Instance.IsOobeBackgroundVisible = true;
		await UiContext.Navigate().To().AddWalletPage().GetResultAsync();
		MainViewModel.Instance.IsOobeBackgroundVisible = false;
	}
}
