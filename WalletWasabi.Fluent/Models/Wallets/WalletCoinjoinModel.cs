using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletCoinjoinModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposable = new();

	private readonly Wallet _wallet;
	private readonly WalletSettingsModel _settings;
	private CoinJoinManager _coinJoinManager;
	[AutoNotify] private bool _isCoinjoining;

	public WalletCoinjoinModel(Wallet wallet, WalletSettingsModel settings)
	{
		_wallet = wallet;
		_settings = settings;
		_coinJoinManager = Services.HostedServices.Get<CoinJoinManager>();

		StatusUpdated =
			Observable.FromEventPattern<StatusChangedEventArgs>(_coinJoinManager, nameof(CoinJoinManager.StatusChanged))
					  .Where(x => x.EventArgs.Wallet == wallet)
					  .Select(x => x.EventArgs)
					  .Where(x => x is WalletStartedCoinJoinEventArgs or WalletStoppedCoinJoinEventArgs or StartErrorEventArgs or CoinJoinStatusEventArgs or CompletedEventArgs or StartedEventArgs)
					  .ObserveOn(RxApp.MainThreadScheduler);

		settings.WhenAnyValue(x => x.AutoCoinjoin)
				.Skip(1) // The first one is triggered at the creation.
				.DoAsync(async (autoCoinJoin) =>
				{
					if (autoCoinJoin)
					{
						await StartAsync(stopWhenAllMixed: false, false);
					}
					else
					{
						await StopAsync();
					}
				})
				.Subscribe()
				.DisposeWith(_disposable);

		var coinjoinInputStarted =
			StatusUpdated
				.OfType<CoinJoinStatusEventArgs>()
				.Where(e => e.CoinJoinProgressEventArgs is EnteringInputRegistrationPhase)
				.Select(_ => true);

		var coinjoinStarted =
			StatusUpdated
				.OfType<StartedEventArgs>()
				.Select(_ => true);

		var coinjoinStopped =
			StatusUpdated
				.OfType<WalletStoppedCoinJoinEventArgs>()
				.Select(_ => false);

		var coinjoinCompleted =
			StatusUpdated
				.OfType<CompletedEventArgs>()
				.Select(_ => false);

		IsRunning =
			coinjoinInputStarted
				.Merge(coinjoinStopped)
				.Merge(coinjoinCompleted)
				.ObserveOn(RxApp.MainThreadScheduler);

		IsRunning
			.BindTo(this, x => x.IsCoinjoining)
			.DisposeWith(_disposable);

		IsStarted =
			coinjoinStarted
				.Merge(coinjoinStopped)
				.ObserveOn(RxApp.MainThreadScheduler);
	}

	public IObservable<StatusChangedEventArgs> StatusUpdated { get; }

	public IObservable<bool> IsRunning { get; }

	public IObservable<bool> IsStarted { get; }

	public async Task StartAsync(bool stopWhenAllMixed, bool overridePlebStop)
	{
		Wallet outputWallet = Services.WalletManager.GetWallets().First(x => x.WalletId == _settings.OutputWalletId);

		await _coinJoinManager.StartAsync(_wallet, outputWallet, stopWhenAllMixed, overridePlebStop, CancellationToken.None);
	}

	public async Task StopAsync()
	{
		await _coinJoinManager.StopAsync(_wallet, CancellationToken.None);
	}

	public void Dispose()
	{
		_ = StopAsync();
		_disposable.Dispose();
	}
}
