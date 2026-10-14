using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Daemon.FeeRateProviders;
using WalletWasabi.Tor.Http;
using Xunit;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Tests.UnitTests;

public class MempoolSpaceFeeRateProviderTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MempoolUsesPreciseFeesWithLegacyFallbackAsync(bool legacy)
	{
		const string json = "{\"fastestFee\":1.125,\"halfHourFee\":0.111,\"hourFee\":0.101,\"economyFee\":0.1}";
		using var preciseResponse = new HttpResponseMessage(legacy ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new StringContent(json) };
		using var legacyResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
		var http = new Mock<IHttpClient>(MockBehavior.Strict);
		http.Setup(x => x.SendAsync(HttpMethod.Get, "fees/precise", null, It.IsAny<CancellationToken>())).ReturnsAsync(preciseResponse);
		if (legacy)
		{
			http.Setup(x => x.SendAsync(HttpMethod.Get, "fees/recommended", null, It.IsAny<CancellationToken>())).ReturnsAsync(legacyResponse);
		}

		var result = await new MempoolSpaceFeeRateProvider(http.Object).GetFeeRatesAsync(CancellationToken.None);

		FeeStrategy.CreateFromFeeRate(result.GetFeeRate(2));
		FeeStrategy.CreateFromFeeRate(result.GetFeeRate(72));
		Assert.Equal(1.125m, result.GetFeeRate(2).SatoshiPerByte);
		Assert.Equal(Constants.MinRelayFeeRate, result.GetFeeRate(72));
		http.Verify(x => x.SendAsync(HttpMethod.Get, "fees/recommended", null, It.IsAny<CancellationToken>()), legacy ? Times.Once() : Times.Never());
	}

	[Fact]
	public async Task ZeroFeeResponseDoesNotBecomeUsableAsync()
	{
		using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"fastestFee\":0,\"halfHourFee\":0,\"hourFee\":0,\"economyFee\":0}") };
		var http = new Mock<IHttpClient>(MockBehavior.Strict);
		http.Setup(x => x.SendAsync(HttpMethod.Get, "fees/precise", null, It.IsAny<CancellationToken>())).ReturnsAsync(response);

		var result = await new MempoolSpaceFeeRateProvider(http.Object).GetFeeRatesAsync(CancellationToken.None);

		Assert.Equal(NBitcoin.FeeRate.Zero, result.GetFeeRate(2));
		Assert.Throws<ArgumentOutOfRangeException>(() => FeeStrategy.CreateFromFeeRate(result.GetFeeRate(2)));
	}
}
