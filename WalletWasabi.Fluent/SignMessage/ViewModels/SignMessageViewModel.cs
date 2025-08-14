using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Userfacing.Bip21;

namespace WalletWasabi.Fluent.SignMessage.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SignMessageViewModel : RoutableViewModel
{
	private readonly WalletModel _walletModel;
	private readonly object _parsingLock = new();

	[AutoNotify] private string _address = "";
	[AutoNotify] private string _messageToSign = "";
	[AutoNotify] private string _signedMessage = "";

	private bool _parsingTo;

	public SignMessageViewModel(WalletModel walletModel)
	{
		_walletModel = walletModel;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		this.ValidateProperty(x => x.Address, ValidateAddressField);

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		PasteMessageCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteMessageAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);
		FindAddressCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.Address, x => x.MessageToSign)
				.Select(x =>
				{
					var allFilled = !string.IsNullOrEmpty(x.Item1) && !string.IsNullOrEmpty(x.Item2);
					var hasError = Validations.Any;

					return allFilled && !hasError;
				});
		NextCommand = ReactiveCommand.CreateFromTask(SignMessageAsync, nextCommandCanExecute);
	}

	public ICommand PasteCommand { get; }
	public ICommand PasteMessageCommand { get; }
	public ICommand AutoPasteCommand { get; }
	public ICommand FindAddressCommand { get; }

	private async Task SignMessageAsync()
	{
		if (string.IsNullOrEmpty(MessageToSign) ||
		    !_walletModel.Addresses.TryGetHdPubKey(Address, out var hdPubKey))
		{
			return;
		}

		try
		{
			if (_walletModel is HardwareWalletModel hardwareWallet)
			{
				SignedMessage = await UiContext.Navigate().To().HardwareWalletMessageSigning(hardwareWallet, MessageToSign, hdPubKey).GetResultAsync() ?? "";
			}
			else
			{
				IsBusy = true;
				SignedMessage = _walletModel.SignMessage(MessageToSign, hdPubKey);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Resources.SignMessage, ex.ToUserFriendlyString(), "");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void ValidateAddressField(IValidationErrors errors)
	{
		if (string.IsNullOrEmpty(Address))
		{
			return;
		}

		if (!string.IsNullOrEmpty(Address) && (Address.IsTrimmable() || !AddressStringParser.TryParse(Address, _walletModel.Network, out _)))
		{
			errors.Add(ErrorSeverity.Error, Resources.InvalidBitcoinAddress);
			return;
		}

		if (!_walletModel.Addresses.TryGetHdPubKey(Address, out _))
		{
			errors.Add(ErrorSeverity.Error, Resources.TheAddressDoesNotBelongToThisWallet);
			return;
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());
		}
	}

	private async Task OnAutoPasteAsync()
	{
		var isAutoPasteEnabled = UiContext.ApplicationSettings.AutoPaste;

		if (string.IsNullOrEmpty(Address) && isAutoPasteEnabled)
		{
			await OnPasteAsync(pasteIfInvalid: false);
		}
	}

	private async Task OnPasteAsync(bool pasteIfInvalid = true)
	{
		var text = await ApplicationHelper.GetTextAsync();

		lock (_parsingLock)
		{
			if (!TryParseUrl(text) && pasteIfInvalid)
			{
				Address = text;
			}
		}
	}

	private async Task OnPasteMessageAsync()
	{
		MessageToSign = await ApplicationHelper.GetTextAsync();
	}

	private bool TryParseUrl(string? text)
	{
		if (_parsingTo)
		{
			return false;
		}

		_parsingTo = true;

		text = text?.Trim();

		if (string.IsNullOrEmpty(text))
		{
			_parsingTo = false;
			return false;
		}

		bool result = false;

		if (AddressStringParser.TryParse(text, _walletModel.Network, out Bip21UriParser.Result? parserResult))
		{
			result = true;

			if (parserResult.Address is { })
			{
				Address = parserResult.Address.ToString();
			}
		}

		Dispatcher.UIThread.Post(() => _parsingTo = false);

		return result;
	}
}
