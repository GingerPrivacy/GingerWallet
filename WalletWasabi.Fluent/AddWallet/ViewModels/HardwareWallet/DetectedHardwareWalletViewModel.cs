using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.AddWallet.ViewModels.HardwareWallet;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class DetectedHardwareWalletViewModel : RoutableViewModel
{
	public DetectedHardwareWalletViewModel(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		Title = Resources.HardwareWallet;

		var (walletName, device) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);

		WalletName = walletName;

		Type = device.WalletType;

		TypeName = device.Model.FriendlyName();

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(options));

		NoCommand = ReactiveCommand.Create(OnNo);

		EnableAutoBusyOn(NextCommand);
	}

	public CancellationTokenSource? CancelCts { get; private set; }

	public string WalletName { get; }

	public WalletType Type { get; }

	public string TypeName { get; }

	public ICommand NoCommand { get; }

	private async Task OnNextAsync(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		try
		{
			CancelCts ??= new CancellationTokenSource();
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options, CancelCts.Token);
			UiContext.Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.ErrorAddingWallet);
			UiContext.Navigate(CurrentTarget).Back();
		}
	}

	private void OnNo()
	{
		UiContext.Navigate(CurrentTarget).Back();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		disposables.Add(Disposable.Create(() =>
		{
			CancelCts?.Cancel();
			CancelCts?.Dispose();
			CancelCts = null;
		}));
	}
}
