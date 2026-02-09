using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using Nito.AsyncEx;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.WebClients.Wasabi;
using GingerCommon.Logging;
using WabiSabi.Helpers;

namespace WalletWasabi.Daemon.FeeRateProviders;

/// <summary>
/// Provides fee rate estimates with automatic background refresh capability.
/// Inherits from BackgroundService for lifecycle management.
/// Thread-safe implementation with configurable refresh intervals.
/// </summary>
public class FeeRateProvider : BackgroundService, IWalletFeeRateProvider
{
	private IFeeRateProvider? _feeRateProvider;

	private readonly AsyncLock _lock = new();
	private readonly object _cacheLock = new();
	private readonly WasabiHttpClientFactory _httpClientFactory;
	private readonly Network _network;
	private readonly TaskCompletionSource _initialized = new();
	private readonly SemaphoreSlim _refreshTrigger = new(0, 1);
	private readonly object _fastRefreshLock = new();

	private AllFeeEstimate? _allFeeEstimate;
	private DateTimeOffset? _lastRefresh;

	private volatile bool _isFastRefresh;
	private volatile bool _disposed;

	/// <summary>
	/// Normal refresh interval (10 minutes).
	/// </summary>
	private static readonly TimeSpan NormalRefreshInterval = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Fast refresh interval used during transactions (1 minute).
	/// </summary>
	private static readonly TimeSpan FastRefreshInterval = TimeSpan.FromMinutes(1);

	public FeeRateProvider(WasabiHttpClientFactory httpClientFactory, FeeRateProviderSource provider, Network network)
	{
		Provider = provider;
		_httpClientFactory = httpClientFactory;
		_network = network;
	}

	public FeeRateProviderSource Provider { get; private set; }

	/// <summary>
	/// Gets or sets whether fast refresh mode is enabled.
	/// When true, refreshes every minute. When false, refreshes every 10 minutes.
	/// Changing this value will interrupt the current wait and apply the new interval.
	/// Thread-safe property.
	/// </summary>
	public bool IsFastRefresh
	{
		get => _isFastRefresh;
		set
		{
			ThrowIfDisposed();

			lock (_fastRefreshLock)
			{
				if (_isFastRefresh != value)
				{
					_isFastRefresh = value;
					Logger.LogInfo($"Fee rate refresh mode changed to: {(value ? "Fast (1 min)" : "Normal (10 min)")}");

					if (value)
					{
						// Interrupt current wait to apply new interval immediately
						TriggerImmediateRefresh();
					}
				}
			}
		}
	}

	/// <summary>
	/// Initializes the fee rate provider based on configuration.
	/// Full node initialization happens later so we initialize here.
	/// Must be called before the service starts.
	/// </summary>
	/// <param name="rpcFeeRateProvider">Optional RPC fee rate provider for full node mode.</param>
	public void Initialize(RpcFeeRateProvider? rpcFeeRateProvider)
	{
		ThrowIfDisposed();

		try
		{
			if (_network == Network.RegTest)
			{
				_feeRateProvider = new RegTestFeeRateProvider();
			}
			else if (Provider == FeeRateProviderSource.BlockstreamInfo)
			{
				_feeRateProvider = new BlockstreamInfoFeeRateProvider(_httpClientFactory, _network);
			}
			else if (Provider == FeeRateProviderSource.FullNode)
			{
				if (rpcFeeRateProvider == null)
				{
					_feeRateProvider = null;
					Logger.LogError($"{nameof(FeeRateProvider)} set to '{Provider}' but {nameof(RpcFeeRateProvider)} is null. Full node seems to be not present.");
				}
				else
				{
					_feeRateProvider = rpcFeeRateProvider;
				}
			}
			else
			{
				if (Provider != FeeRateProviderSource.MempoolSpace)
				{
					Logging.Logger.LogWarning($"{nameof(FeeRateProvider)} config is missing or erroneous - falling back to '{FeeRateProviderSource.MempoolSpace}'.");
					Provider = FeeRateProviderSource.MempoolSpace;
				}
				_feeRateProvider = new MempoolSpaceFeeRateProvider(_httpClientFactory, _network);
			}

			Logger.LogInfo($"{nameof(FeeRateProvider)} initialized to '{Provider}'.");
		}
		finally
		{
			_initialized.SetResult();
		}
	}

	/// <summary>
	/// Used for tests.
	/// </summary>
	public FeeRateProvider(WasabiHttpClientFactory httpClientFactory, Network network)
		: this(httpClientFactory, FeeRateProviderSource.BlockstreamInfo, network)
	{
		Initialize(null);
	}

	/// <summary>
	/// Gets the current fee estimate from cache.
	/// Returns immediately without blocking - safe to call from UI thread.
	/// Thread-safe synchronous access to cached values.
	/// </summary>
	/// <returns>Cached fee estimate.</returns>
	/// <exception cref="InvalidOperationException">If provider is not initialized or cache is empty.</exception>
	/// <exception cref="ObjectDisposedException">If provider is disposed.</exception>
	public AllFeeEstimate GetAllFeeEstimate()
	{
		ThrowIfDisposed();

		if (_feeRateProvider is null)
		{
			throw new InvalidOperationException($"{nameof(FeeRateProvider)} is null. Call Initialize first.");
		}

		// Synchronous cache read with dedicated lock
		lock (_cacheLock)
		{
			return GetCachedValueOrThrow();
		}
	}

	/// <summary>
	/// Triggers an immediate refresh by interrupting the current wait period.
	/// The refresh loop will immediately proceed to the next refresh cycle.
	/// Non-blocking - returns immediately.
	/// Thread-safe and can be called multiple times safely.
	/// </summary>
	public void TriggerImmediateRefresh()
	{
		if (_disposed)
		{
			return; // Silently ignore if disposed
		}

		try
		{
			// Release semaphore to signal immediate refresh
			// If already released (CurrentCount == 1), this will be ignored
			if (_refreshTrigger.CurrentCount == 0)
			{
				_refreshTrigger.Release();
				Logger.LogDebug("Immediate refresh triggered.");
			}
		}
		catch (ObjectDisposedException)
		{
			// Semaphore disposed during shutdown, ignore
		}
		catch (SemaphoreFullException)
		{
			// Already signaled, ignore
		}
	}

	/// <summary>
	/// Returns the cached fee estimate value or throws if not available.
	/// Must be called within a lock.
	/// </summary>
	/// <returns>Cached fee estimate.</returns>
	/// <exception cref="InvalidOperationException">If cache is not initialized.</exception>
	private AllFeeEstimate GetCachedValueOrThrow()
	{
		if (_allFeeEstimate is null)
		{
			throw new InvalidOperationException("Fee rate cache is not initialized. Waiting for first refresh cycle.");
		}

		return _allFeeEstimate;
	}

	/// <summary>
	/// Refreshes the fee rate cache from the provider.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task RefreshCacheAsync(CancellationToken cancellationToken)
	{
		// Wait for initialization to complete
		await _initialized.Task.ConfigureAwait(false);

		if (_feeRateProvider is null)
		{
			throw new InvalidOperationException($"{nameof(FeeRateProvider)} is null. Cannot refresh.");
		}

		// Use async lock for the actual refresh operation
		using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var result = await _feeRateProvider.GetFeeRatesAsync(cancellationToken).ConfigureAwait(false);

			// Update cache atomically with simple lock
			lock (_cacheLock)
			{
				_lastRefresh = DateTimeOffset.UtcNow;
				_allFeeEstimate = result;
			}

			Logger.LogDebug($"Fee rates refreshed successfully at {_lastRefresh:yyyy-MM-dd HH:mm:ss}");
		}
	}

	/// <summary>
	/// Main background refresh loop - executed by BackgroundService.
	/// Continuously refreshes fee rates at configured intervals.
	/// Can be interrupted for immediate refresh via TriggerImmediateRefresh().
	/// </summary>
	/// <param name="stoppingToken">Cancellation token for stopping the service.</param>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		Logger.LogInfo("Fee rate refresh loop started.");

		// Wait for initialization before starting
		try
		{
			await _initialized.Task.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Initialization failed: {ex}");
			return;
		}

		// Perform initial refresh immediately
		try
		{
			await RefreshCacheAsync(stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			Logger.LogInfo("Fee rate refresh loop cancelled during initial refresh.");
			return;
		}
		catch (Exception ex)
		{
			Logger.LogError($"Initial fee rate refresh failed: {ex}");
		}

		// Main refresh loop
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Determine current refresh interval based on mode
				var interval = _isFastRefresh ? FastRefreshInterval : NormalRefreshInterval;

				// Wait for interval OR immediate refresh trigger OR stopping
				var waitTask = _refreshTrigger.WaitAsync(interval, stoppingToken);

				try
				{
					await waitTask.ConfigureAwait(false);

					// If we got here, immediate refresh was triggered
					Logger.LogDebug("Wait period interrupted for immediate refresh.");
				}
				catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
				{
					// Timeout expired (normal interval), proceed to refresh
				}

				// Check if we're stopping before attempting refresh
				if (stoppingToken.IsCancellationRequested)
				{
					break;
				}

				// Perform the refresh
				await RefreshCacheAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Service is stopping
				break;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error during fee rate refresh: {ex}");

				// Add a delay before retry to prevent tight loop on persistent errors
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		Logger.LogInfo("Fee rate refresh loop stopped.");
	}

	/// <summary>
	/// Throws ObjectDisposedException if disposed.
	/// </summary>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(FeeRateProvider));
		}
	}

	/// <summary>
	/// Disposes resources when the service is stopped.
	/// </summary>
	public override void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			_refreshTrigger?.Dispose();
		}
		catch (Exception ex)
		{
			Logger.LogError($"Error disposing refresh trigger: {ex}");
		}

		try
		{
			base.Dispose();
		}
		catch (Exception ex)
		{
			Logger.LogError($"Error in base.Dispose: {ex}");
		}

		Logger.LogInfo($"{nameof(FeeRateProvider)} disposed.");
	}
}
