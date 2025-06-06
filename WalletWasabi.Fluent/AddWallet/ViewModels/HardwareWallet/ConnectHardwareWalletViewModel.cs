using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.AddWallet.ViewModels.HardwareWallet;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ConnectHardwareWalletViewModel : RoutableViewModel
{
	private readonly WalletCreationOptions.ConnectToHardwareWallet _options;
	[AutoNotify] private string _message;
	[AutoNotify] private bool _isSearching;
	[AutoNotify] private bool _existingWalletFound;
	[AutoNotify] private bool _confirmationRequired;

	public ConnectHardwareWalletViewModel(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		Title = Lang.Resources.HardwareWallet;

		_options = options;

		ArgumentException.ThrowIfNullOrEmpty(options.WalletName);
		_message = "";
		WalletName = options.WalletName;
		AbandonedTasks = new AbandonedTasks();
		CancelCts = new CancellationTokenSource();

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(OnNext);

		NavigateToExistingWalletLoginCommand = ReactiveCommand.Create(OnNavigateToExistingWalletLogin);

		this.WhenAnyValue(x => x.Message)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(message => ConfirmationRequired = !string.IsNullOrEmpty(message));
	}

	private HwiEnumerateEntry? DetectedDevice { get; set; }

	public CancellationTokenSource CancelCts { get; set; }

	private AbandonedTasks AbandonedTasks { get; }

	public string WalletName { get; }

	public WalletModel? ExistingWallet { get; set; }

	public ICommand NavigateToExistingWalletLoginCommand { get; }

	public WalletType Ledger => WalletType.Ledger;

	public WalletType Coldcard => WalletType.Coldcard;

	public WalletType Trezor => WalletType.Trezor;

	public WalletType Generic => WalletType.Hardware;

	private void OnNext()
	{
		if (DetectedDevice is { } device)
		{
			NavigateToNext(device);
			return;
		}

		StartDetection();
	}

	private void OnNavigateToExistingWalletLogin()
	{
		if (ExistingWallet is { })
		{
			UiContext.Navigate(CurrentTarget).Clear();
			UiContext.Navigate().To(ExistingWallet);
		}
	}

	private void StartDetection()
	{
		Message = "";

		if (IsSearching)
		{
			return;
		}

		DetectedDevice = null;
		ExistingWalletFound = false;
		AbandonedTasks.AddAndClearCompleted(DetectionAsync(CancelCts.Token));
	}

	private async Task DetectionAsync(CancellationToken cancel)
	{
		IsSearching = true;

		try
		{
			var result = await UiContext.HardwareWalletInterface.DetectAsync(cancel);
			EvaluateDetectionResult(result, cancel);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError(ex);
		}
		finally
		{
			IsSearching = false;
		}
	}

	private void EvaluateDetectionResult(HwiEnumerateEntry[] devices, CancellationToken cancel)
	{
		if (devices.Length == 0)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModelConnectPc;
			return;
		}

		if (devices.Length > 1)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModelMakeSureOnlyOne;
			return;
		}

		var device = devices[0];

		var existingWallet = UiContext.WalletRepository.GetExistingWallet(device);
		if (existingWallet is { })
		{
			ExistingWallet = existingWallet;
			Message = Lang.Resources.ConnectHardwareWalletViewModelAlreadyAdded;
			ExistingWalletFound = true;
			return;
		}

		if (!device.IsInitialized())
		{
			if (device.Model == HardwareWalletModels.Coldcard)
			{
				Message = Resources.InitializeDeviceFirst;
			}
			else
			{
				Message = Resources.CheckDeviceAndFinishInitialization;
				AbandonedTasks.AddAndClearCompleted(UiContext.HardwareWalletInterface.InitHardwareWalletAsync(device, cancel));
			}

			return;
		}

		if (device.Code is { })
		{
			Message = Resources.SomethingHappenedWithDevice;
			return;
		}

		if (device.NeedsPassphraseSent == true)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModelEnterPassphraseOnDevice;
			return;
		}

		if (device.NeedsPinSent == true)
		{
			Message = Lang.Resources.ConnectHardwareWalletViewModelEnterPin;
			return;
		}

		DetectedDevice = device;

		if (!ConfirmationRequired)
		{
			NavigateToNext(DetectedDevice);
		}
	}

	private void NavigateToNext(HwiEnumerateEntry device)
	{
		UiContext.Navigate().To().DetectedHardwareWallet(_options with { Device = device });
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;

		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		if (isInHistory)
		{
			CancelCts = new CancellationTokenSource();
		}

		StartDetection();

		disposables.Add(Disposable.Create(async () =>
		{
			CancelCts.Cancel();
			await AbandonedTasks.WhenAllAsync();
			CancelCts.Dispose();
		}));
	}
}
