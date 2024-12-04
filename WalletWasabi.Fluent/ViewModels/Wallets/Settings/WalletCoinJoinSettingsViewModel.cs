using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 1,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true,
	Searchable = false)]
public partial class WalletCoinJoinSettingsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private string? _selectedCoinjoinProfileName;
	[AutoNotify] private IWalletModel _selectedOutputWallet;
	[AutoNotify] private ReadOnlyObservableCollection<IWalletModel> _wallets = ReadOnlyObservableCollection<IWalletModel>.Empty;
	[AutoNotify] private bool _isOutputWalletSelectionEnabled = true;
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private string _anonScoreTarget;
	[AutoNotify] private string _safeMiningFeeRate;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;
	[AutoNotify] private SkipFactorItem[] _skipFactors;
	[AutoNotify] private SkipFactorItem _selectedSkipFactors;

	[AutoNotify] private bool _useExperimentalCoinSelector;


	[AutoNotify] private bool _ignoreCostOptimizationVisible;

	private CompositeDisposable _disposable = new();

	public WalletCoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
	{
		UiContext = uiContext;
		_wallet = walletModel;
		_isCoinjoinProfileSelected = _wallet.Settings.IsCoinjoinProfileSelected;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget.ToString(CultureInfo.InvariantCulture);
		_selectedOutputWallet = UiContext.WalletRepository.Wallets.Items.First(x => x.Id == _wallet.Settings.OutputWalletId);
		_redCoinIsolation = _wallet.Settings.RedCoinIsolation;
		_safeMiningFeeRate = _wallet.Settings.SafeMiningFeeRate.ToString(CultureInfo.InvariantCulture);

		_timeFrames =
		[
			new TimeFrameItem(Resources.Hours, 0),
			new TimeFrameItem(Resources.Days, WalletWasabi.Helpers.Constants.CoinJoinFeeRateMedianTimeFrames[0]),
			new TimeFrameItem(Resources.Weeks, WalletWasabi.Helpers.Constants.CoinJoinFeeRateMedianTimeFrames[1]),
			new TimeFrameItem(Resources.Months, WalletWasabi.Helpers.Constants.CoinJoinFeeRateMedianTimeFrames[2])
		];
		_selectedTimeFrame = _timeFrames.FirstOrDefault(tf => tf.Value == _wallet.Settings.FeeRateMedianTimeFrameHours) ?? _timeFrames.First();
		_ignoreCostOptimizationVisible = _selectedTimeFrame.Value != 0;

		_skipFactors =
		[
			new SkipFactorItem(Resources.Disabled, CoinjoinSkipFactors.NoSkip),
			new SkipFactorItem(Resources.Rarely, CoinjoinSkipFactors.SpeedMaximizing),
			new SkipFactorItem(Resources.Sometimes, CoinjoinSkipFactors.PrivacyMaximizing),
			new SkipFactorItem(Resources.Often, CoinjoinSkipFactors.CostMinimizing),
		];
		_selectedSkipFactors = _skipFactors.FirstOrDefault(x => x.Factors == _wallet.Settings.CoinjoinSkipFactors) ?? _skipFactors.First();

		_useExperimentalCoinSelector = _wallet.Settings.UseExperimentalCoinSelector;


		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;
		CoinjoinConfigurationCommand =
			ReactiveCommand.Create(() => UiContext.Navigate().To().CoinjoinCoinSelectorSettings(walletModel));

		this.ValidateProperty(x => x.AnonScoreTarget, x => ValidateInteger(x, AnonScoreTarget, 2, 1000));


		this.WhenAnyValue(x => x.IsCoinjoinProfileSelected)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.IsCoinjoinProfileSelected = x;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.AutoCoinJoin)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.AutoCoinjoin = x;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.PlebStopThreshold)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
				{
					if (Money.TryParse(x, out var result) && result != _wallet.Settings.PlebStopThreshold)
					{
						_wallet.Settings.PlebStopThreshold = result;
						_wallet.Settings.Save();
					}
				});

		this.WhenAnyValue(x => x.AnonScoreTarget)
			.Skip(1)
			.Where(_ => !HasError(nameof(AnonScoreTarget)))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (int.TryParse(x, out var result) && result != _wallet.Settings.AnonScoreTarget)
				{
					_wallet.Settings.AnonScoreTarget = result;
					_wallet.Settings.Save();
				}
			});

		this.WhenAnyValue(x => x.SelectedOutputWallet)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x => _wallet.Settings.OutputWalletId = x.Id);

		this.WhenAnyValue(x => x.RedCoinIsolation)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.RedCoinIsolation = x;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.SafeMiningFeeRate)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (int.TryParse(x, out var result) && result != _wallet.Settings.SafeMiningFeeRate)
				{
					_wallet.Settings.SafeMiningFeeRate = result;
					_wallet.Settings.Save();
				}
			});

		this.WhenAnyValue(x => x.SelectedTimeFrame)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.FeeRateMedianTimeFrameHours = x.Value;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.SelectedTimeFrame)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				IgnoreCostOptimizationVisible = x.Value != 0;
			});

		this.WhenAnyValue(x => x.SelectedSkipFactors)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.CoinjoinSkipFactors = x.Factors;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.UseExperimentalCoinSelector)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.UseExperimentalCoinSelector = x;
				_wallet.Settings.Save();
			});

		walletModel.Coinjoin.IsStarted
			.Select(isRunning => !isRunning)
			.BindTo(this, x => x.IsOutputWalletSelectionEnabled);

		ManuallyUpdateOutputWalletList();
	}

	public ICommand CoinjoinConfigurationCommand { get; }

	private void ValidateInteger(IValidationErrors errors, string value, int min, int max)
	{
		if (!int.TryParse(value, out var result))
		{
			errors.Add(ErrorSeverity.Error, Resources.ValidationErrorNotNumber);
			return;
		}
		if (result < min || result > max)
		{
			errors.Add(ErrorSeverity.Error, error: string.Format(CultureInfo.InvariantCulture, Resources.ValidationErrorNotInRange, min, max));
			return;
		}
	}

	public void ManuallyUpdateOutputWalletList()
	{
		_disposable.Dispose();
		_disposable = new CompositeDisposable();

		UiContext.WalletRepository.Wallets
			.Connect()
			.AutoRefresh(x => x.IsLoaded)
			.Filter(x => (x.Id == _wallet.Id || x.Settings.OutputWalletId != _wallet.Id) && x.IsLoaded)
			.SortBy(i => i.Name)
			.Bind(out var wallets)
			.Subscribe()
			.DisposeWith(_disposable);

		_wallets = wallets;
	}

	public record TimeFrameItem(string Name, int Value)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	public record SkipFactorItem(string Name, CoinjoinSkipFactors Factors)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
