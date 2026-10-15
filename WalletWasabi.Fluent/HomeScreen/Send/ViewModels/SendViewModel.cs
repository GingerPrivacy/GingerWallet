using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Labels.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Userfacing;
using WalletWasabi.Userfacing.Bip21;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

[NavigationMetaData(
	IconName = "wallet_action_send",
	Order = 5,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false,
	IsLocalized = true)]
public partial class SendViewModel : RoutableViewModel
{
	private readonly object _parsingLock = new();
	private readonly Wallet _wallet;
	private readonly WalletModel _walletModel;
	private readonly SendFlowModel _parameters;
	private readonly bool _continueWithFixedAmount;
	private readonly CoinJoinManager? _coinJoinManager;
	private readonly ClipboardObserver _clipboardObserver;

	private bool _parsingTo;

	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _isFixedAmount;
	[AutoNotify] private bool _isPayJoin;
	[AutoNotify] private string? _payJoinEndPoint;
	[AutoNotify] private bool _conversionReversed;
	[AutoNotify] private bool _subtractFee;
	[AutoNotify] private bool _batchCanContinue;
	[AutoNotify] private string? _batchError;

	public SendViewModel(WalletModel walletModel, SendFlowModel parameters, LabelsArray? preLabel = null, bool continueWithFixedAmount = false)
	{
		_wallet = parameters.Wallet;
		_walletModel = walletModel;
		_parameters = parameters;
		_continueWithFixedAmount = continueWithFixedAmount;
		_coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		_conversionReversed = UiContext.ApplicationSettings.SendAmountConversionReversed;

		ExchangeRate = _walletModel.AmountProvider.ExchangeRate;
		FiatTicker = Resources.Culture.GetFiatTicker();

		Balance =
			_parameters.IsManual
				? Observable.Return(_walletModel.AmountProvider.Create(_parameters.AvailableAmount))
				: _walletModel.Balances;

		Recipients.Add(CreateRecipient(preLabel));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = parameters.IsManual;

		this.ValidateProperty(x => x.To, ValidateToField);
		this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Where(_ => !IsPayToMany)
			.Subscribe(ParseToField);

		this.WhenAnyValue(x => x.PayJoinEndPoint)
			.Subscribe(endPoint => IsPayJoin = endPoint is { });

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);
		InsertMaxCommand = ReactiveCommand.Create(() => AmountBtc = _parameters.AvailableAmountBtc);
		QrCommand = ReactiveCommand.Create(ShowQrCameraAsync);
		PayToManyCommand = ReactiveCommand.Create(AddRecipient,
			this.WhenAnyValue(x => x.IsPayJoin, x => x.IsFixedAmount)
				.Select(x => !x.Item1 && !x.Item2 && !_continueWithFixedAmount));

		var nextCommandCanExecute =
			this.WhenAnyValue(
					x => x.AmountBtc,
					x => x.To,
					x => x.SuggestionLabels.Labels.Count,
					x => x.SuggestionLabels.IsCurrentTextValid,
					x => x.IsPayToMany,
					x => x.BatchCanContinue)
				.Select(tup =>
				{
					var (amountBtc, to, labelsCount, isCurrentTextValid, isPayToMany, batchCanContinue) = tup;
					var allFilled = !string.IsNullOrEmpty(to) && amountBtc > 0;
					var hasError = Validations.Any;

					return isPayToMany ? batchCanContinue : allFilled && !hasError && (labelsCount > 0 || isCurrentTextValid);
				});

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		this.WhenAnyValue(x => x.ConversionReversed)
			.Skip(1)
			.Subscribe(x => UiContext.ApplicationSettings.SendAmountConversionReversed = x);

		_clipboardObserver = new ClipboardObserver(Balance);
	}

	public IObservable<Amount> Balance { get; }

	public ObservableCollection<RecipientRowViewModel> Recipients { get; } = new();
	public bool IsPayToMany => Recipients.Count > 1;
	public string EditorTitle => IsPayToMany ? Resources.BatchPayments : Resources.WalletSend;
	public string To
	{
		get => Recipients[0].To;
		set => Recipients[0].To = value;
	}
	public decimal? AmountBtc
	{
		get => Recipients[0].AmountBtc;
		set => Recipients[0].AmountBtc = value;
	}
	public SuggestionLabelsViewModel SuggestionLabels => Recipients[0].SuggestionLabels;

	public string FiatTicker { get; }

	public IObservable<string?> FiatContent => _clipboardObserver.ClipboardFiatContentChanged(RxApp.MainThreadScheduler);

	public IObservable<string?> BitcoinContent => _clipboardObserver.ClipboardBtcContentChanged(RxApp.MainThreadScheduler);

	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	public ICommand InsertMaxCommand { get; }

	public ICommand PayToManyCommand { get; }

	private RecipientRowViewModel CreateRecipient(LabelsArray? labels = null) =>
		new(_walletModel, OnRecipientChanged, RemoveRecipient, InsertMax, labels);

	private void AddRecipient()
	{
		SubtractFee |= AmountBtc == _parameters.AvailableAmountBtc;
		Recipients.Add(CreateRecipient());
		UpdateRecipientCount();
	}

	private void RemoveRecipient(RecipientRowViewModel row)
	{
		if (Recipients.Count > 1 && Recipients.Remove(row))
		{
			row.Dispose();
			this.RaisePropertyChanged(nameof(To));
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(SuggestionLabels));
			UpdateRecipientCount();
		}
	}

	private void UpdateRecipientCount()
	{
		this.RaisePropertyChanged(nameof(IsPayToMany));
		this.RaisePropertyChanged(nameof(EditorTitle));
		ValidateRecipients();
	}

	private void OnRecipientChanged(RecipientRowViewModel row, string? propertyName)
	{
		if (Recipients.Count == 0)
		{
			return;
		}
		if (row == Recipients[0] && propertyName is { })
		{
			this.RaisePropertyChanged(propertyName);
		}
		ValidateRecipients();
	}

	private void InsertMax(RecipientRowViewModel row)
	{
		var otherAmounts = Recipients.Where(x => x != row).Sum(x => x.AmountBtc ?? 0);
		row.AmountBtc = Math.Max(0, _parameters.AvailableAmountBtc - otherAmounts);
		SubtractFee = true;
	}

	private IReadOnlyList<RecipientInfo>? GetRecipients()
	{
		var recipients = new List<RecipientInfo>();
		foreach (var row in Recipients)
		{
			if (row.TryGetRecipient(out var recipient) && recipient is { })
			{
				recipients.Add(recipient);
			}
		}
		return recipients.Count == Recipients.Count ? recipients : null;
	}

	private void ValidateRecipients()
	{
		BatchError = null;
		var recipients = IsPayToMany ? GetRecipients() : null;
		if (recipients is { })
		{
			if (recipients.Select(x => x.Destination.ScriptPubKey).Distinct().Count() != recipients.Count)
			{
				BatchError = Resources.BatchDuplicateAddress;
			}
			else if (recipients.Sum(x => x.Amount) > _parameters.AvailableAmount)
			{
				BatchError = Resources.InsufficientFunds;
			}
		}
		BatchCanContinue = recipients is { Count: > 1 } && BatchError is null;
	}

	protected virtual async Task OnPreviewAsync(TransactionInfo transactionInfo)
	{
		if (_coinJoinManager is { } coinJoinManager)
		{
			await coinJoinManager.WalletEnteredSendingAsync(_wallet);
		}
		UiContext.Navigate().To().TransactionPreview(_walletModel, _parameters with { TransactionInfo = transactionInfo });
	}

	private async Task OnNextAsync()
	{
		if (IsPayToMany)
		{
			ValidateRecipients();
			if (BatchCanContinue && GetRecipients() is { } recipients && recipients[0].Destination is BitcoinAddress destination)
			{
				await OnPreviewAsync(new TransactionInfo(destination, _walletModel.Settings.AnonScoreTarget)
				{
					Amount = recipients[0].Amount,
					Recipient = recipients[0].Label,
					AdditionalRecipients = recipients.Skip(1).ToArray(),
					SubtractFee = SubtractFee,
					IsFixedAmount = true
				});
			}
			return;
		}

		var label = new LabelsArray(SuggestionLabels.Labels.ToArray());

		if (AmountBtc is not { } amountBtc)
		{
			return;
		}

		var amount = new Money(amountBtc, MoneyUnit.BTC);
		var transactionInfo = new TransactionInfo(BitcoinAddress.Create(To, _walletModel.Network), _walletModel.Settings.AnonScoreTarget)
		{
			Amount = amount,
			Recipient = label,
			PayJoinClient = GetPayjoinClient(PayJoinEndPoint),
			IsFixedAmount = IsFixedAmount || _continueWithFixedAmount,
			SubtractFee = amount == _parameters.AvailableCoins.TotalAmount() && !(IsFixedAmount || IsPayJoin)
		};

		await OnPreviewAsync(transactionInfo);
	}

	private async Task OnAutoPasteAsync()
	{
		var isAutoPasteEnabled = UiContext.ApplicationSettings.AutoPaste;

		if (!IsPayToMany && string.IsNullOrEmpty(To) && isAutoPasteEnabled)
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
				To = text;
			}
		}
	}

	private bool _payjoinDisableOutputSubstitution;

	private IPayjoinClient? GetPayjoinClient(string? endPoint)
	{
		if (!string.IsNullOrWhiteSpace(endPoint) &&
			Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
		{
			var payjoinEndPointUri = new Uri(endPoint);
			if (!PayjoinClient.IsEndpointSupported(payjoinEndPointUri, _walletModel.Network, Services.Config.UseTor != TorMode.Disabled))
			{
				Logger.LogWarning("Payjoin server must use HTTPS or an onion service with Tor enabled. Ignoring...");
				return null;
			}

			IHttpClient httpClient = Services.HttpClientFactory.NewHttpClient(() => payjoinEndPointUri, Mode.DefaultCircuit);
			return new PayjoinClient(payjoinEndPointUri, httpClient, _payjoinDisableOutputSubstitution);
		}

		return null;
	}

	private async Task ShowQrCameraAsync()
	{
		var result = await UiContext.Navigate().To().ShowQrCameraDialog(_walletModel.Network).GetResultAsync();
		if (!string.IsNullOrWhiteSpace(result))
		{
			To = result;
		}
	}

	private void ValidateAmount(IValidationErrors errors)
	{
		if (AmountBtc is null)
		{
			return;
		}

		if (AmountBtc > Constants.MaximumNumberOfBitcoins)
		{
			errors.Add(ErrorSeverity.Error, Resources.AmountLessThanTotalSupply);
		}
		else if (AmountBtc > _parameters.AvailableAmountBtc)
		{
			errors.Add(ErrorSeverity.Error, Resources.InsufficientFunds);
		}
		else if (AmountBtc <= 0)
		{
			errors.Add(ErrorSeverity.Error, Resources.AmountMoreThanZero);
		}
	}

	private void ValidateToField(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(To) && (To.IsTrimmable() || !AddressStringParser.TryParse(To, _walletModel.Network, out _)))
		{
			errors.Add(ErrorSeverity.Error, Resources.InvalidBTCAddressOrURI);
		}
		else if (IsPayJoin && _walletModel.IsHardwareWallet)
		{
			errors.Add(ErrorSeverity.Error, Resources.PayjoinNotPossibleWithHardwareWallets);
		}
	}

	private void ParseToField(string s)
	{
		lock (_parsingLock)
		{
			Dispatcher.UIThread.Post(() => TryParseUrl(s));
		}
	}

	private bool TryParseUrl(string? text)
	{
		if (_parsingTo || IsPayToMany)
		{
			return false;
		}

		_parsingTo = true;

		text = text?.Trim();

		if (string.IsNullOrEmpty(text))
		{
			_parsingTo = false;
			PayJoinEndPoint = null;
			IsFixedAmount = false;
			return false;
		}

		_payjoinDisableOutputSubstitution = false;
		bool result = false;

		if (AddressStringParser.TryParse(text, _walletModel.Network, out Bip21UriParser.Result? parserResult))
		{
			result = true;

			PayJoinEndPoint = parserResult.UnknownParameters.TryGetValue("pj", out var endPoint) ? endPoint : null;
			_payjoinDisableOutputSubstitution = parserResult.UnknownParameters.TryGetValue("pjos", out var outputSubstitution) && outputSubstitution == "0";

			if (parserResult.Address is { })
			{
				To = parserResult.Address.ToString();
			}

			if (parserResult.Amount is { })
			{
				AmountBtc = parserResult.Amount.ToDecimal(MoneyUnit.BTC);
				IsFixedAmount = true;
			}
			else
			{
				IsFixedAmount = false;
			}

			if (parserResult.Label is { } parsedLabel)
			{
				SuggestionLabels.Labels.Clear();
				SuggestionLabels.Labels.Add(parsedLabel);
			}
		}
		else
		{
			IsFixedAmount = false;
			PayJoinEndPoint = null;
		}

		Dispatcher.UIThread.Post(() => _parsingTo = false);

		return result;
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		if (!inHistory)
		{
			To = "";
			AmountBtc = 0;
			ClearValidations();

			if (_coinJoinManager is { } coinJoinManager)
			{
				coinJoinManager.WalletEnteredSendWorkflow(_walletModel.Id);
			}
		}

		_walletModel.AmountProvider.ExchangeRateObservable
			.BindTo(this, x => x.ExchangeRate)
			.DisposeWith(disposables);

		RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());

		_wallet.FeeProvider.IsFastRefresh = true;

		base.OnNavigatedTo(inHistory, disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		if (!isInHistory)
		{
			foreach (var recipient in Recipients)
			{
				recipient.Dispose();
			}
			_coinJoinManager?.WalletLeftSendWorkflow(_wallet);
			_wallet.FeeProvider.IsFastRefresh = false;
		}
	}
}
