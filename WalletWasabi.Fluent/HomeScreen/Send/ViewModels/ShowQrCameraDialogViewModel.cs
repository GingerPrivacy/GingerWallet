using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Lang;
using WalletWasabi.Userfacing;
using WalletWasabi.Userfacing.Bip21;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	private readonly Network _network;
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _errorMessage = "";
	[AutoNotify] private string _qrContent = "";

	public ShowQrCameraDialogViewModel(Network network)
	{
		Title = Resources.Camera;

		_network = network;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		UiContext.QrCodeReader
			.Read()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(
				onNext: result =>
				{
					if (AddressStringParser.TryParse(result.decoded, _network, out Bip21UriParser.Result? parserResult, out string? errorMessage))
					{
						Close(DialogResultKind.Normal, result.decoded);
					}
					else
					{
						// Remember last error message and last QR content.
						if (errorMessage is not null)
						{
							if (!string.IsNullOrEmpty(result.decoded))
							{
								ErrorMessage = errorMessage;
							}
						}

						if (!string.IsNullOrEmpty(result.decoded))
						{
							QrContent = result.decoded;
						}

						// ... but show always the current bitmap.
						QrImage = result.bitmap;
					}
				},
				onError: error => Dispatcher.UIThread.Post(async () =>
					{
						Close();
						await ShowErrorAsync(Title, error.Message, "", NavigationTarget.CompactDialogScreen);
					}))
			.DisposeWith(disposables);
	}
}
