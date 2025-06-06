using System.Globalization;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.State;
using WalletWasabi.Lang;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.Fluent.HomeScreen.CoinjoinPlayer.ViewModel;

[AppLifetime]
public partial class CoinjoinPlayerViewModel : ViewModelBase
{
	private static string CountDownMessage = Resources.CountDownMessage;
	private static string WaitingMessage = Resources.WaitingMessage;
	private static string UneconomicalRoundMessage = Resources.UneconomicalRoundMessage;
	private static string RandomlySkippedRoundMessage = Resources.RandomlySkippedRoundMessage;
	private static string PauseMessage = Resources.PauseMessage;
	private static string StoppedMessage = Resources.StoppedMessage;
	private static string PressPlayToStartMessage = Resources.PressPlayToStartMessage;
	private static string RoundSucceedMessage = Resources.RoundSucceedMessage;
	private static string RoundFinishedMessage = Resources.RoundFinishedMessage;
	private static string AbortedNotEnoughAlicesMessage = Resources.AbortedNotEnoughAlicesMessage;
	private static string CoinJoinInProgress = Resources.CoinJoinInProgress;
	private static string InputRegistrationMessage = Resources.InputRegistrationMessage;
	private static string WaitingForBlameRoundMessage = Resources.WaitingForBlameRoundMessage;
	private static string WaitingRoundMessage = Resources.WaitingRoundMessage;
	private static string PlebStopMessage = Resources.PlebStopMessage;
	private static string PlebStopMessageBelow = Resources.PlebStopMessageBelow;
	private static string NoCoinsEligibleToMixMessage = Resources.NoCoinsEligibleToMixMessage;
	private static string UserInSendWorkflowMessage = Resources.UserInSendWorkflowMessage;
	private static string AllPrivateMessage = Resources.AllPrivateMessage;
	private static string BackendNotConnected = Resources.BackendNotConnected;
	private static string GeneralErrorMessage = Resources.GeneralErrorMessage;
	private static string WaitingForConfirmedFunds = Resources.WaitingForConfirmedFunds;
	private static string CoinsRejectedMessage = Resources.CoinsRejectedMessage;
	private static string OnlyImmatureCoinsAvailableMessage = Resources.OnlyImmatureCoinsAvailableMessage;
	private static string OnlyExcludedCoinsAvailableMessage = Resources.OnlyExcludedCoinsAvailableMessage;
	private static string MiningFeeRateTooHighMessage = Resources.MiningFeeRateTooHighMessage;
	private static string CoordinationFeeRateTooHighMessage = Resources.CoordinationFeeRateTooHighMessage;
	private static string MinInputCountTooLowMessage = Resources.MinInputCountTooLowMessage;
	private static string ServerDidNotGiveFeeExemptionMessage = Resources.ServerDidNotGiveFeeExemptionMessage;

	private readonly WalletModel _wallet;
	private readonly StateMachine<State, Trigger> _stateMachine;
	private readonly DispatcherTimer _countdownTimer;
	private readonly DispatcherTimer _autoCoinJoinStartTimer;

	[AutoNotify] private bool _isAutoWaiting;
	[AutoNotify] private bool _playVisible;
	[AutoNotify] private bool _pauseVisible;
	[AutoNotify] private bool _pauseSpreading;
	[AutoNotify] private bool _stopVisible;
	[AutoNotify] private string _currentStatus = "";
	[AutoNotify] private bool _isProgressReversed;
	[AutoNotify] private double _progressValue;
	[AutoNotify] private string _leftText = "";
	[AutoNotify] private string _rightText = "";
	[AutoNotify] private bool _isInCriticalPhase;
	[AutoNotify] private bool _isCountDownDelayHappening;
	[AutoNotify] private bool _areAllCoinsPrivate;

	private DateTimeOffset _countDownStartTime;
	private DateTimeOffset _countDownEndTime;

	public CoinjoinPlayerViewModel(WalletModel wallet, WalletSettingsViewModel settings)
	{
		_wallet = wallet;

		wallet.Coinjoin.StatusUpdated
					   .Do(ProcessStatusChange)
					   .Subscribe();

		wallet.Privacy.IsWalletPrivate
					  .BindTo(this, x => x.AreAllCoinsPrivate);

		var initialState =
			wallet.Settings.AutoCoinjoin
			? State.WaitingForAutoStart
			: State.StoppedOrPaused;

		if (wallet.IsHardwareWallet || wallet.IsWatchOnlyWallet)
		{
			initialState = State.Disabled;
		}

		if (wallet.Settings.IsCoinJoinPaused)
		{
			initialState = State.StoppedOrPaused;
		}

		_stateMachine = new StateMachine<State, Trigger>(initialState);

		ConfigureStateMachine();

		wallet.Balances
			  .Do(_ => _stateMachine.Fire(Trigger.BalanceChanged))
			  .Subscribe();

		this.WhenAnyValue(x => x.AreAllCoinsPrivate)
			.Do(_ => _stateMachine.Fire(Trigger.AreAllCoinsPrivateChanged))
			.Subscribe();

		PlayCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var overridePlebStop = _stateMachine.IsInState(State.PlebStopActive);
			await wallet.Coinjoin.StartAsync(stopWhenAllMixed: !IsAutoCoinJoinEnabled, overridePlebStop);
		});

		var stopPauseCommandCanExecute =
			this.WhenAnyValue(
				x => x.IsInCriticalPhase,
				x => x.PauseSpreading,
				(isInCriticalPhase, pauseSpreading) => !isInCriticalPhase && !pauseSpreading);

		StopPauseCommand = ReactiveCommand.CreateFromTask(wallet.Coinjoin.StopAsync, stopPauseCommandCanExecute);

		AutoCoinJoinObservable = wallet.Settings.WhenAnyValue(x => x.AutoCoinjoin);

		AutoCoinJoinObservable
			.Skip(1) // The first one is triggered at the creation.
			.Where(x => !x)
			.Do(_ => _stateMachine.Fire(Trigger.AutoCoinJoinOff))
			.Subscribe();

		wallet.Settings.WhenAnyValue(x => x.PlebStopThreshold)
					   .SubscribeAsync(async _ =>
					   {
						   // Hack: we take the value from KeyManager but it is saved later.
						   await Task.Delay(1500);
						   _stateMachine.Fire(Trigger.PlebStopChanged);
					   });

		_autoCoinJoinStartTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Random.Shared.Next(5, 16)) };
		_autoCoinJoinStartTimer.Tick += async (_, _) =>
		{
			await wallet.Coinjoin.StartAsync(stopWhenAllMixed: false, false);
			_autoCoinJoinStartTimer.Stop();
		};

		_countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_countdownTimer.Tick += (_, _) => _stateMachine.Fire(Trigger.Tick);

		_stateMachine.Start();

		var coinJoinSettingsCommand = ReactiveCommand.Create(
			() =>
			{
				settings.SelectedTab = 1;
				UiContext.Navigate(NavigationTarget.DialogScreen).To(settings);
			},
			Observable.Return(!_wallet.IsWatchOnlyWallet));

		NavigateToSettingsCommand = coinJoinSettingsCommand;
		CanNavigateToCoinjoinSettings = coinJoinSettingsCommand.CanExecute;
		NavigateToExcludedCoinsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().ExcludedCoins(_wallet));
	}

	private enum State
	{
		Invalid = 0,
		Disabled,
		StoppedOrPaused,
		Playing,
		PlebStopActive,
		WaitingForAutoStart,
	}

	private enum Trigger
	{
		Invalid = 0,
		PlebStopActivated,
		StartError,
		BalanceChanged,
		Tick,
		PlebStopChanged,
		WalletStartedCoinJoin,
		WalletStoppedCoinJoin,
		AutoCoinJoinOff,
		AreAllCoinsPrivateChanged
	}

	public IObservable<bool> CanNavigateToCoinjoinSettings { get; }

	public ICommand NavigateToSettingsCommand { get; }

	public ICommand NavigateToExcludedCoinsCommand { get; }

	public bool IsAutoCoinJoinEnabled => _wallet.Settings.AutoCoinjoin;

	public IObservable<bool> AutoCoinJoinObservable { get; }

	private bool IsCountDownFinished => GetRemainingTime() <= TimeSpan.Zero;

	private bool IsCounting => _countdownTimer.IsEnabled;

	public ICommand PlayCommand { get; }

	public ICommand StopPauseCommand { get; }

	private void ConfigureStateMachine()
	{
		_stateMachine.Configure(State.Disabled);

		_stateMachine.Configure(State.WaitingForAutoStart)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.AutoCoinJoinOff, State.StoppedOrPaused)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.Permit(Trigger.StartError, State.Playing)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;
				IsAutoWaiting = true;

				var now = DateTimeOffset.UtcNow;
				var autoStartEnd = now + _autoCoinJoinStartTimer.Interval;
				_autoCoinJoinStartTimer.Start();

				StartCountDown(CountDownMessage, now, autoStartEnd);
			})
			.OnExit(() =>
			{
				IsAutoWaiting = false;
				_autoCoinJoinStartTimer.Stop();
				StopCountDown();
			})
			.OnTrigger(Trigger.Tick, UpdateCountDown);

		_stateMachine.Configure(State.StoppedOrPaused)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				StopCountDown();
				PauseVisible = false;
				PauseSpreading = false;
				StopVisible = false;

				// PlayVisible, CurrentStatus and LeftText set inside.
				RefreshButtonAndTextInStateStoppedOrPaused();
			})
			.OnTrigger(Trigger.AreAllCoinsPrivateChanged, () =>
			{
				// Refresh the UI according to AreAllCoinsPrivate, the play button and the left-text.
				RefreshButtonAndTextInStateStoppedOrPaused();
			})
			.OnExit(() => LeftText = "");

		_stateMachine.Configure(State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
			.Permit(Trigger.PlebStopActivated, State.PlebStopActive)
			.OnEntry(() =>
			{
				PlayVisible = false;
				PauseVisible = IsAutoCoinJoinEnabled;
				StopVisible = !IsAutoCoinJoinEnabled;

				CurrentStatus = WaitingMessage;
			})
			.OnTrigger(Trigger.Tick, UpdateCountDown);

		_stateMachine.Configure(State.PlebStopActive)
			.Permit(Trigger.BalanceChanged, State.Playing)
			.Permit(Trigger.PlebStopChanged, State.Playing)
			.Permit(Trigger.WalletStartedCoinJoin, State.Playing)
			.Permit(Trigger.WalletStoppedCoinJoin, State.StoppedOrPaused)
			.Permit(Trigger.StartError, State.Playing)
			.OnEntry(() =>
			{
				PlayVisible = true;
				PauseVisible = false;
				StopVisible = false;

				CurrentStatus = PlebStopMessage;
				LeftText = PlebStopMessageBelow;
			})
			.OnExit(() => LeftText = "");
	}

	private void RefreshButtonAndTextInStateStoppedOrPaused()
	{
		if (IsAutoCoinJoinEnabled)
		{
			PlayVisible = true;
			CurrentStatus = PauseMessage;
			LeftText = PressPlayToStartMessage;
		}
		else if (AreAllCoinsPrivate)
		{
			PlayVisible = false;
			LeftText = "";
			CurrentStatus = AllPrivateMessage;
		}
		else
		{
			PlayVisible = true;
			CurrentStatus = StoppedMessage;
			LeftText = PressPlayToStartMessage;
		}
	}

	private void UpdateCountDown()
	{
		IsCountDownDelayHappening = IsCounting && IsCountDownFinished;

		// This case mostly happens when there is some delay between the client and the server,
		// and the countdown has finished but the client hasn't received any new phase changed message.
		if (IsCountDownDelayHappening)
		{
			LeftText = Resources.WaitingForResponse;
			RightText = "";
			return;
		}

		var format = @"hh\:mm\:ss";
		LeftText = $"{GetElapsedTime().ToString(format, CultureInfo.InvariantCulture)}";
		RightText = $"-{GetRemainingTime().ToString(format, CultureInfo.InvariantCulture)}";
		ProgressValue = GetPercentage();
	}

	private TimeSpan GetElapsedTime() => DateTimeOffset.UtcNow - _countDownStartTime;

	private TimeSpan GetRemainingTime() => _countDownEndTime - DateTimeOffset.UtcNow;

	private TimeSpan GetTotalTime() => _countDownEndTime - _countDownStartTime;

	private double GetPercentage() => GetElapsedTime().TotalSeconds / GetTotalTime().TotalSeconds * 100;

	private void ProcessStatusChange(StatusChangedEventArgs e)
	{
		switch (e)
		{
			case WalletStartedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStartedCoinJoin);
				break;

			case WalletStoppedCoinJoinEventArgs:
				_stateMachine.Fire(Trigger.WalletStoppedCoinJoin);
				break;

			case StartErrorEventArgs start:
				if (start.Error is CoinjoinError.NotEnoughUnprivateBalance)
				{
					_stateMachine.Fire(Trigger.PlebStopActivated);
					break;
				}

				_stateMachine.Fire(Trigger.StartError);
				CurrentStatus = start.Error switch
				{
					CoinjoinError.NoCoinsEligibleToMix => NoCoinsEligibleToMixMessage,
					CoinjoinError.NoConfirmedCoinsEligibleToMix => WaitingForConfirmedFunds,
					CoinjoinError.UserInSendWorkflow => UserInSendWorkflowMessage,
					CoinjoinError.AllCoinsPrivate => AllPrivateMessage,
					CoinjoinError.UserWasntInRound => RoundFinishedMessage,
					CoinjoinError.BackendNotSynchronized => BackendNotConnected,
					CoinjoinError.CoinsRejected => CoinsRejectedMessage,
					CoinjoinError.OnlyImmatureCoinsAvailable => OnlyImmatureCoinsAvailableMessage,
					CoinjoinError.OnlyExcludedCoinsAvailable => OnlyExcludedCoinsAvailableMessage,
					CoinjoinError.UneconomicalRound => UneconomicalRoundMessage,
					CoinjoinError.RandomlySkippedRound => RandomlySkippedRoundMessage,
					CoinjoinError.MiningFeeRateTooHigh => MiningFeeRateTooHighMessage,
					CoinjoinError.CoordinationFeeRateTooHigh => CoordinationFeeRateTooHighMessage,
					CoinjoinError.MinInputCountTooLow => MinInputCountTooLowMessage,
					CoinjoinError.ServerDidNotGiveFeeExemption => ServerDidNotGiveFeeExemptionMessage,
					_ => GeneralErrorMessage
				};

				StopCountDown();
				break;

			case CoinJoinStatusEventArgs coinJoinStatusEventArgs:
				OnCoinJoinPhaseChanged(coinJoinStatusEventArgs.CoinJoinProgressEventArgs);
				break;
		}
	}

	private void OnCoinJoinPhaseChanged(CoinJoinProgressEventArgs coinJoinProgress)
	{
		switch (coinJoinProgress)
		{
			case RoundEnded roundEnded:
				if (roundEnded.IsStopped)
				{
					PauseSpreading = true;
				}
				else
				{
					CurrentStatus = roundEnded.LastRoundState.EndRoundState switch
					{
						EndRoundState.TransactionBroadcasted => RoundSucceedMessage,
						EndRoundState.AbortedNotEnoughAlices => AbortedNotEnoughAlicesMessage,
						_ => RoundFinishedMessage
					};
					StopCountDown();
				}
				break;

			case EnteringOutputRegistrationPhase outputRegPhase:
				_countDownEndTime = outputRegPhase.TimeoutAt + outputRegPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout;
				break;

			case EnteringSigningPhase signingPhase:
				_countDownEndTime = signingPhase.TimeoutAt;
				break;

			case EnteringInputRegistrationPhase inputRegPhase:
				StartCountDown(
					message: InputRegistrationMessage,
					start: inputRegPhase.TimeoutAt - inputRegPhase.RoundState.InputRegistrationTimeout,
					end: inputRegPhase.TimeoutAt);
				break;

			case WaitingForBlameRound waitingForBlameRound:
				StartCountDown(message: WaitingForBlameRoundMessage, start: DateTimeOffset.UtcNow, end: waitingForBlameRound.TimeoutAt);
				break;

			case WaitingForRound:
				CurrentStatus = WaitingRoundMessage;
				StopCountDown();
				break;

			case EnteringConnectionConfirmationPhase confirmationPhase:

				var startTime = confirmationPhase.TimeoutAt - confirmationPhase.RoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;
				var totalEndTime = confirmationPhase.TimeoutAt +
								   confirmationPhase.RoundState.CoinjoinState.Parameters.OutputRegistrationTimeout +
								   confirmationPhase.RoundState.CoinjoinState.Parameters.TransactionSigningTimeout;

				StartCountDown(
					message: CoinJoinInProgress,
					start: startTime,
					end: totalEndTime);

				break;

			case EnteringCriticalPhase:
				IsInCriticalPhase = true;
				break;

			case LeavingCriticalPhase:
				IsInCriticalPhase = false;
				break;
		}
	}

	private void StartCountDown(string message, DateTimeOffset start, DateTimeOffset end)
	{
		CurrentStatus = message;
		_countDownStartTime = start;
		_countDownEndTime = end;
		UpdateCountDown(); // force the UI to apply the changes at the same time.
		_countdownTimer.Start();
	}

	private void StopCountDown()
	{
		_countdownTimer.Stop();
		IsCountDownDelayHappening = false;
		_countDownStartTime = DateTimeOffset.MinValue;
		_countDownEndTime = DateTimeOffset.MinValue;
		LeftText = "";
		RightText = "";
		ProgressValue = 0;
	}
}
