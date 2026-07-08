using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Daemon.FeeRateProviders;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class FeeRateProviderTests
{
	[Fact]
	public async Task FullNodeWithoutRpcProviderThrowsClearUnavailableErrorAsync()
	{
		await using var httpClientFactory = CreateHttpClientFactory();
		using var feeRateProvider = new FeeRateProvider(httpClientFactory, FeeRateProviderSource.FullNode, Network.Main);

		feeRateProvider.Initialize(rpcFeeRateProvider: null);

		var exception = Assert.Throws<InvalidOperationException>(feeRateProvider.GetAllFeeEstimate);
		Assert.Equal(FeeRateProvider.FullNodeFeeEstimatesUnavailableMessage, exception.Message);
	}

	[Fact]
	public async Task FullNodeWithoutRpcProviderCanStartAndStopAsync()
	{
		await using var httpClientFactory = CreateHttpClientFactory();
		using var feeRateProvider = new FeeRateProvider(httpClientFactory, FeeRateProviderSource.FullNode, Network.Main);

		feeRateProvider.Initialize(rpcFeeRateProvider: null);

		var startException = await Record.ExceptionAsync(() => feeRateProvider.StartAsync(CancellationToken.None));
		Assert.Null(startException);

		var stopException = await Record.ExceptionAsync(() => feeRateProvider.StopAsync(CancellationToken.None));
		Assert.Null(stopException);
	}

	private static WasabiHttpClientFactory CreateHttpClientFactory()
		=> new(torEndPoint: null, backendUriGetter: () => new Uri("http://localhost/"));
}
