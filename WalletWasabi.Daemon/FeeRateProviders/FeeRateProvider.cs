using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Nito.AsyncEx;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.WebClients.Wasabi;
using GingerCommon.Logging;
using WabiSabi.Helpers;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Daemon.FeeRateProviders;

public class FeeRateProvider : IWalletFeeRateProvider
{
	private IFeeRateProvider? _feeRateProvider;

	private readonly AsyncLock _lock = new();
	private readonly WasabiHttpClientFactory _httpClientFactory;
	private readonly Network _network;
	private readonly TaskCompletionSource _initialized = new();

	public FeeRateProvider(WasabiHttpClientFactory httpClientFactory, FeeRateProviderSource provider, Network network)
	{
		Provider = provider;
		_httpClientFactory = httpClientFactory;
		_network = network;
	}

	public FeeRateProviderSource Provider { get; private set; }

	/// <summary>
	/// Full node initialization happens later so we initialize here.
	/// </summary>
	/// <param name="rpcFeeRateProvider"></param>
	public void Initialize(RpcFeeRateProvider? rpcFeeRateProvider)
	{
		try
		{
			// We always respect the user choice, otherwise throw error.

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
					Logging.Logger.LogWarning($"{nameof(FeeRateProvider)} config is missing or errorneus - falling back to '{FeeRateProviderSource.MempoolSpace}'.");
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
	public FeeRateProvider(WasabiHttpClientFactory httpClientFactory, Network network) : this(httpClientFactory, FeeRateProviderSource.BlockstreamInfo, network)
	{
	}

	public AllFeeEstimate GetAllFeeEstimate()
	{
		if (_feeRateProvider is null)
		{
			throw new InvalidOperationException($"{nameof(FeeRateProvider)} is null.");
		}

		using (_lock.Lock())
		{
			var task = Task.Run(async () => await GetCacheAsync(CancellationToken.None).ConfigureAwait(false));
			task.Wait();
			return task.Result;
		}
	}

	public async Task<AllFeeEstimate> GetAllFeeEstimateAsync(CancellationToken cancellationToken)
	{
		if (_feeRateProvider is null)
		{
			throw new InvalidOperationException($"{nameof(FeeRateProvider)} is null.");
		}

		using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return await GetCacheAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public void TriggerRefresh()
	{
		Task.Run(() => GetCacheAsync(CancellationToken.None, true));
	}

	private AllFeeEstimate? _allFeeEstimate;
	private DateTimeOffset? _lastFee;

	private async Task<AllFeeEstimate> GetCacheAsync(CancellationToken cancellationToken, bool forceRefresh = false)
	{
		Guard.NotNull(nameof(_feeRateProvider), _feeRateProvider);

		if (!NeedsRefresh() && !forceRefresh)
		{
			return _allFeeEstimate;
		}

		await _initialized.Task.ConfigureAwait(false);

		var result = await _feeRateProvider.GetFeeRatesAsync(cancellationToken).ConfigureAwait(false);
		_lastFee = DateTimeOffset.Now;
		_allFeeEstimate = result;
		return result;
	}

	[MemberNotNullWhen(false, nameof(_allFeeEstimate))]
	private bool NeedsRefresh()
	{
		if (_lastFee is null || _allFeeEstimate is null)
		{
			return true;
		}

		if (DateTimeOffset.UtcNow - _lastFee < TimeSpan.FromMinutes(1))
		{
			return false;
		}

		return true;
	}
}
