using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SellViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;
	[AutoNotify] private CountrySelection? _selectedCountry;
	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private bool _conversionReversed;
	[AutoNotify] private string? _amount;
	[AutoNotify] private decimal _minAmount;
	[AutoNotify] private decimal _maxAmount;
	[AutoNotify] private bool _fetchingLimits;
	[AutoNotify] private bool _hasPreviousOrders;
	[AutoNotify] private bool _hasOrderOnHold;
	[AutoNotify] private CurrencyModel[] _currencies = [];
	[AutoNotify] private CurrencyModel? _selectedCurrency;

	private CountryModel[] _availableCountries = [];

	public SellViewModel(WalletModel wallet)
	{
		_wallet = wallet;
		Title = Resources.SellBitcoin;
		_selectedCountry = UiContext.ApplicationSettings.GetCurrentSellCountry();
		ExchangeRate = wallet.AmountProvider.ExchangeRate;
		_conversionReversed = Services.UiConfig.SendAmountConversionReversed; // must be fixed
		FiatTicker = Resources.Culture.GetFiatTicker();

		var nextCanExecute =
			this.WhenAnyValue(x => x.Amount, x => x.FetchingLimits, x => x.MinAmount, x => x.MaxAmount)
				.Select(x =>
				{
					var (amount, fetchingLimits, min, max) = x;

					if (!decimal.TryParse(amount, out var decimalAmount))
					{
						return false;
					}

					return decimalAmount >= min && decimalAmount <= max && !fetchingLimits;
				});
		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (SelectedCurrency is null || SelectedCountry is null || !decimal.TryParse(Amount, out var amount))
			{
				return;
			}

			try
			{
				IsBusy = true;
				var offers = await _wallet.BuySellModel.GetSellOffersAsync("BTC", SelectedCurrency.Ticker, amount, SelectedCountry.CountryCode, SelectedCountry.StateCode);

				if (offers.Length == 0)
				{
					await ShowErrorAsync(Title, Resources.NoOfferForAmount, "");
					return;
				}

				UiContext.Navigate().To().SellOffers(_wallet, offers);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "");
			}
			finally
			{
				IsBusy = false;
			}
		}, nextCanExecute);
		SelectCountryCommand = ReactiveCommand.CreateFromTask(async () => await ShowSelectCountryDialogAsync(UiContext.ApplicationSettings.GetCurrentSellCountry()));
		PreviousOrdersCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Orders(_wallet.BuySellModel, OrderType.Sell));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		this.ValidateProperty(x => x.Amount, ValidateAmount);

		this.WhenAnyValue(x => x.SelectedCurrency)
			.WhereNotNull()
			.Where(x => !Equals(x, UiContext.ApplicationSettings.GetCurrentSellCurrency()))
			.DoAsync(async c =>
			{
				UiContext.ApplicationSettings.SetSellCurrency(c);
				await SetLimitsAsync();
				this.RaisePropertyChanged(nameof(Amount));
			})
			.Subscribe();

		this.WhenAnyValue(x => x.SelectedCountry)
			.WhereNotNull()
			.Where(_ => SelectedCurrency is not null)
			.DoAsync(async c =>
			{
				await SetLimitsAsync();
				this.RaisePropertyChanged(nameof(Amount));
			})
			.Subscribe();
	}

	public ICommand SelectCountryCommand { get; }
	public ICommand PreviousOrdersCommand { get; }

	public string FiatTicker { get; }

	private void ValidateAmount(IValidationErrors errors)
	{
		if (string.IsNullOrEmpty(Amount))
		{
			return;
		}

		if (!decimal.TryParse(Amount, out var decimalAmount))
		{
			errors.Add(ErrorSeverity.Error, Resources.ValidationErrorNotNumber);
		}

		if (decimalAmount > MaxAmount)
		{
			errors.Add(ErrorSeverity.Error, Resources.AmountCannotExceed.SafeInject(new Amount(MaxAmount).FormattedBtcWithUnit));
		}
		else if (decimalAmount > _wallet.Coins.List.Items.Sum(x => x.Amount))
		{
			errors.Add(ErrorSeverity.Error, Resources.InsufficientFunds);
		}
		else if (decimalAmount < MinAmount)
		{
			errors.Add(ErrorSeverity.Error, Resources.AmountMustBeAtLeast.SafeInject(new Amount(MinAmount).FormattedBtcWithUnit));
		}
	}

	private async Task<bool> ShowSelectCountryDialogAsync(CountrySelection? selectedCountry = null)
	{
		var country = await UiContext.Navigate().To().SelectCountry(_availableCountries, selectedCountry).GetResultAsync();
		if (country is not null)
		{
			UiContext.ApplicationSettings.SetSellCountry(country);
			SelectedCountry = country;
		}

		return country is not null;
	}

	private async Task<bool> EnsureSelectedCountryAsync()
	{
		var current = UiContext.ApplicationSettings.GetCurrentSellCountry();
		_availableCountries = await _wallet.BuySellModel.GetSellCountriesAsync();

		var currentCountryFound =
			current is { } &&
			_availableCountries.Any(c =>
			{
				if (c.Code != current.CountryCode)
				{
					return false;
				}

				if (c.States is { } state && state.All(s => s.Code != current.StateCode))
				{
					return false;
				}

				return true;
			});

		if (!currentCountryFound)
		{
			if (!await ShowSelectCountryDialogAsync())
			{
				CancelCommand.ExecuteIfCan();
				return false;
			}
		}

		return true;
	}

	private async Task EnsureSelectedCurrencyAsync()
	{
		Currencies = await _wallet.BuySellModel.GetSellCurrenciesAsync();
		SelectedCurrency = UiContext.ApplicationSettings.GetCurrentSellCurrency();

		if (SelectedCurrency is null || (SelectedCurrency is not null && !Currencies.Contains(SelectedCurrency)))
		{
			SelectedCurrency = Currencies.FirstOrDefault(x => x.Ticker == UiContext.ApplicationSettings.SelectedExchangeCurrency) ??
			                   Currencies.FirstOrDefault(x => x.Ticker == GingerCultureInfo.DefaultFiatCurrencyTicker) ??
			                   Currencies.First();
		}
	}

	private async Task SetLimitsAsync()
	{
		try
		{
			FetchingLimits = true;

			if (SelectedCurrency is not { } currency || SelectedCountry is not { } country)
			{
				throw new InvalidOperationException("Missing currency or country");
			}

			var (min, max) = await _wallet.BuySellModel.GetSellLimitsAsync(currency.Ticker, country.CountryCode, country.StateCode);

			MinAmount = min;
			MaxAmount = max;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			MinAmount = 0;
			MaxAmount = decimal.MaxValue;
		}
		finally
		{
			FetchingLimits = false;
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		this.WhenAnyObservable(x => x._wallet.BuySellModel.HasAnySellOrder)
			.BindTo(this, x => x.HasPreviousOrders)
			.DisposeWith(disposables);

		this.WhenAnyObservable(x => x._wallet.BuySellModel.HasSellOrderOnHold)
			.BindTo(this, x => x.HasOrderOnHold)
			.DisposeWith(disposables);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				try
				{
					IsBusy = true;

					if (!await EnsureSelectedCountryAsync())
					{
						return;
					}

					await EnsureSelectedCurrencyAsync();
					await SetLimitsAsync();
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					UiContext.Navigate(CurrentTarget).Clear();
					await ShowErrorAsync(Resources.Sell, Resources.ServiceNotAvailable, "");
				}
				finally
				{
					IsBusy = false;
				}
			});
		}
	}
}
