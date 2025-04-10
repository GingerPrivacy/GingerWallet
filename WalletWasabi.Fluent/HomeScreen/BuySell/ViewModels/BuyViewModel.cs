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
public partial class BuyViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private CountrySelection? _selectedCountry;
	[AutoNotify] private string? _amount;
	[AutoNotify] private decimal _minAmount;
	[AutoNotify] private decimal _maxAmount;
	[AutoNotify] private bool _fetchingLimits;
	[AutoNotify] private bool _hasPreviousOrders;
	[AutoNotify] private bool _hasOrderOnHold;
	[AutoNotify] private CurrencyModel[] _currencies = [];
	[AutoNotify] private CurrencyModel? _selectedCurrency;

	private CountryModel[] _availableCountries = [];

	public BuyViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		Title = Resources.BuyBitcoin;
		_selectedCountry = UiContext.ApplicationSettings.GetCurrentBuyCountry();

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
				var offers = await _wallet.BuySellModel.GetBuyOffersAsync(SelectedCurrency.Ticker, "BTC", amount, SelectedCountry.CountryCode, SelectedCountry.StateCode);

				if (offers.Length == 0)
				{
					await ShowErrorAsync(Resources.BuyBitcoin, Resources.NoOfferForAmount, "");
					return;
				}

				UiContext.Navigate().To().BuyOffers(_wallet, offers);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Resources.BuyBitcoin, ex.ToUserFriendlyString(), "");
			}
			finally
			{
				IsBusy = false;
			}
		}, nextCanExecute);
		SelectCountryCommand = ReactiveCommand.CreateFromTask(async () => await ShowSelectCountryDialogAsync(UiContext.ApplicationSettings.GetCurrentBuyCountry()));
		PreviousOrdersCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Orders(_wallet.BuySellModel, OrderType.Buy));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		this.ValidateProperty(x => x.Amount, ValidateAmount);

		this.WhenAnyValue(x => x.SelectedCurrency)
			.WhereNotNull()
			.Where(x => !Equals(x, UiContext.ApplicationSettings.GetCurrentBuyCurrency()))
			.DoAsync(async c =>
			{
				UiContext.ApplicationSettings.SetBuyCurrency(c);
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
			errors.Add(ErrorSeverity.Error,Resources.AmountCannotExceed.SafeInject(MaxAmount.ToFormattedFiat(SelectedCurrency?.Ticker)));
		}
		else if (decimalAmount < MinAmount)
		{
			errors.Add(ErrorSeverity.Error, Resources.AmountMustBeAtLeast.SafeInject(MinAmount.ToFormattedFiat(SelectedCurrency?.Ticker)));
		}
	}

	private async Task<bool> ShowSelectCountryDialogAsync(CountrySelection? selectedCountry = null)
	{
		var country = await UiContext.Navigate().To().SelectCountry(_availableCountries, selectedCountry).GetResultAsync();
		if (country is not null)
		{
			UiContext.ApplicationSettings.SetBuyCountry(country);
			SelectedCountry = country;
		}

		return country is not null;
	}

	private async Task<bool> EnsureSelectedCountryAsync()
	{
		var current = UiContext.ApplicationSettings.GetCurrentBuyCountry();
		_availableCountries = await _wallet.BuySellModel.GetBuyCountriesAsync();

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
		Currencies = await _wallet.BuySellModel.GetBuyCurrenciesAsync();
		SelectedCurrency = UiContext.ApplicationSettings.GetCurrentBuyCurrency();

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

			var (min, max) = await _wallet.BuySellModel.GetBuyLimitsAsync(currency.Ticker, country.CountryCode, country.StateCode);

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

		this.WhenAnyObservable(x => x._wallet.BuySellModel.HasAnyBuyOrder)
			.BindTo(this, x => x.HasPreviousOrders)
			.DisposeWith(disposables);

		this.WhenAnyObservable(x => x._wallet.BuySellModel.HasBuyOrderOnHold)
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
					Navigate().Clear();
					await ShowErrorAsync(Resources.Buy, Resources.ServiceNotAvailable, "");
				}
				finally
				{
					IsBusy = false;
				}
			});
		}
	}
}
