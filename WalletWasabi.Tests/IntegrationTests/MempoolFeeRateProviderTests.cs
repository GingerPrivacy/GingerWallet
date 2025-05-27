using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Daemon.FeeRateProviders;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class FeeRateProviderTests : IAsyncLifetime
{
	private WasabiHttpClientFactory TorHttpClientFactory { get; }
	private WasabiHttpClientFactory ClearNetHttpClientFactory { get; }
	private TorProcessManager TorProcessManager { get; }

	public FeeRateProviderTests()
	{
		TorProcessManager = new(Common.TorSettings);
		TorHttpClientFactory = new(Common.TorSocks5Endpoint, backendUriGetter: null);
		ClearNetHttpClientFactory = new(null, backendUriGetter: null);
	}

	[Fact]
	public async Task MempoolSpaceTestAsync()
	{
		MempoolSpaceFeeRateProvider provider = new(TorHttpClientFactory, NBitcoin.Network.Main);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	[Fact]
	public async Task MempoolSpaceNoTorTestAsync()
	{
		MempoolSpaceFeeRateProvider provider = new(ClearNetHttpClientFactory, NBitcoin.Network.Main);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	[Fact]
	public async Task MempoolSpaceTestNetTestAsync()
	{
		MempoolSpaceFeeRateProvider provider = new(TorHttpClientFactory, NBitcoin.Network.TestNet);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	[Fact]
	public async Task BlockstreamInfoTestAsync()
	{
		BlockstreamInfoFeeRateProvider provider = new(TorHttpClientFactory, NBitcoin.Network.Main);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	[Fact]
	public async Task BlockstreamInfoTestNetTestAsync()
	{
		BlockstreamInfoFeeRateProvider provider = new(TorHttpClientFactory, NBitcoin.Network.TestNet);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	[Fact]
	public async Task BlockstreamInfoNoTorTestAsync()
	{
		BlockstreamInfoFeeRateProvider provider = new(ClearNetHttpClientFactory, NBitcoin.Network.Main);
		AllFeeEstimate result = await provider.GetFeeRatesAsync(CancellationToken.None);
		Assert.NotEmpty(result.Estimations);
	}

	public async Task InitializeAsync()
	{
		using CancellationTokenSource startTimeoutCts = new(TimeSpan.FromMinutes(2));

		await TorProcessManager.StartAsync(startTimeoutCts.Token);
	}

	public async Task DisposeAsync()
	{
		await TorHttpClientFactory.DisposeAsync();
		await TorProcessManager.DisposeAsync();
	}
}
