using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ReceiveAddressViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;

	public ReceiveAddressViewModel(WalletModel wallet, AddressModel model, bool isAutoCopyEnabled)
	{
		_wallet = wallet;
		Model = model;
		Address = model.Text;
		Type = model.Type.Name;
		Labels = model.Labels;
		IsHardwareWallet = wallet.IsHardwareWallet;
		IsAutoCopyEnabled = isAutoCopyEnabled;
		QrCode = UiContext.QrCodeGenerator.Generate(model.Text.ToUpperInvariant());

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UiContext.Clipboard.SetTextAsync(Address));
		ShowOnHwWalletCommand = ReactiveCommand.CreateFromTask(ShowOnHwWalletAsync);
		NextCommand = CancelCommand;

		if (IsAutoCopyEnabled)
		{
			CopyAddressCommand.Execute(null);
		}
	}

	public bool IsAutoCopyEnabled { get; }

	public ICommand CopyAddressCommand { get; }

	public ICommand ShowOnHwWalletCommand { get; }

	public string Address { get; }

	public string Type { get; }

	public LabelsArray Labels { get; }

	public bool IsHardwareWallet { get; }

	public IObservable<bool[,]> QrCode { get; }

	private AddressModel Model { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Addresses.Unused
			.ToObservableChangeSet()
			.ObserveOn(RxApp.MainThreadScheduler)
			.OnItemRemoved(
				address =>
				{
					if (Equals(address, Model))
					{
						UiContext.Navigate(CurrentTarget).BackTo<ReceiveViewModel>();
					}
				})
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task ShowOnHwWalletAsync()
	{
		try
		{
			await Model.ShowOnHwWalletAsync();
		}
		catch (Exception ex)
		{
			await ShowErrorAsync(Resources.AddressAwaitingPayment, ex.ToUserFriendlyString(), Resources.UnableToSendAddressToDevice);
		}
	}
}
