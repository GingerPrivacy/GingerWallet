using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WalletWasabi.Daemon.FeeRateProviders;
using WalletWasabi.Tor.Http;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class MempoolSpaceFeeRateProviderTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MempoolUsesPreciseFeesWithLegacyFallbackAsync(bool legacy)
	{
		const string json = "{\"fastestFee\":0.125,\"halfHourFee\":0.111,\"hourFee\":0.101,\"economyFee\":0.1}";
		using var preciseResponse = new HttpResponseMessage(legacy ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new StringContent(json) };
		using var legacyResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
		var http = new Mock<IHttpClient>(MockBehavior.Strict);
		http.Setup(x => x.SendAsync(HttpMethod.Get, "fees/precise", null, It.IsAny<CancellationToken>())).ReturnsAsync(preciseResponse);
		if (legacy)
		{
			http.Setup(x => x.SendAsync(HttpMethod.Get, "fees/recommended", null, It.IsAny<CancellationToken>())).ReturnsAsync(legacyResponse);
		}

		var result = await new MempoolSpaceFeeRateProvider(http.Object).GetFeeRatesAsync(CancellationToken.None);

		Assert.Equal(0.125m, result.Estimations[2].SatoshiPerByte);
		Assert.Equal(0.1m, result.Estimations[72].SatoshiPerByte);
		http.Verify(x => x.SendAsync(HttpMethod.Get, "fees/recommended", null, It.IsAny<CancellationToken>()), legacy ? Times.Once() : Times.Never());
	}
}
