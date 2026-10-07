using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Daemon.FeeRateProviders;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class FallbackFeeRateProviderTests
{
	private static AllFeeEstimate ValidEstimate() => new(new Dictionary<int, FeeRate> { [2] = new(2m) });

	[Fact]
	public async Task PreferredProviderDoesNotQueryFallbackAsync()
	{
		var primary = new Mock<IFeeRateProvider>();
		var fallback = new Mock<IFeeRateProvider>(MockBehavior.Strict);
		var estimate = ValidEstimate();
		primary.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimate);

		Assert.Same(estimate, await new FallbackFeeRateProvider(primary.Object, fallback.Object).GetFeeRatesAsync(CancellationToken.None));
		fallback.VerifyNoOtherCalls();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FailedOrTimedOutProviderUsesFallbackAsync(bool timeout)
	{
		var primary = new Mock<IFeeRateProvider>();
		var fallback = new Mock<IFeeRateProvider>();
		Exception error = timeout ? new OperationCanceledException() : new InvalidOperationException("Unavailable");
		primary.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);
		var estimate = ValidEstimate();
		fallback.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimate);

		Assert.Same(estimate, await new FallbackFeeRateProvider(primary.Object, fallback.Object).GetFeeRatesAsync(CancellationToken.None));
	}

	[Theory]
	[InlineData(2, 0)]
	[InlineData(2000, 2)]
	public async Task UnusableEstimateUsesFallbackAsync(int target, int rate)
	{
		var primary = new Mock<IFeeRateProvider>();
		var fallback = new Mock<IFeeRateProvider>();
		primary.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AllFeeEstimate(new Dictionary<int, FeeRate> { [target] = new(rate) }));
		var estimate = ValidEstimate();
		fallback.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estimate);

		Assert.Same(estimate, await new FallbackFeeRateProvider(primary.Object, fallback.Object).GetFeeRatesAsync(CancellationToken.None));
	}

	[Fact]
	public async Task AllProvidersFailWithoutReturningFakeEstimatesAsync()
	{
		var provider = new Mock<IFeeRateProvider>();
		var error = new InvalidOperationException("Unavailable");
		provider.Setup(x => x.GetFeeRatesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);

		var result = await Assert.ThrowsAsync<InvalidOperationException>(() => new FallbackFeeRateProvider(provider.Object).GetFeeRatesAsync(CancellationToken.None));
		Assert.Same(error, result.InnerException);
	}

	[Fact]
	public async Task CancellationDoesNotQueryFallbackEvenIfPrimaryIgnoresTokenAsync()
	{
		using var cancellation = new CancellationTokenSource();
		var primary = new Mock<IFeeRateProvider>();
		var fallback = new Mock<IFeeRateProvider>(MockBehavior.Strict);
		var pending = new TaskCompletionSource<AllFeeEstimate>(TaskCreationOptions.RunContinuationsAsynchronously);
		primary.Setup(x => x.GetFeeRatesAsync(cancellation.Token)).Returns(pending.Task);
		var operation = new FallbackFeeRateProvider(primary.Object, fallback.Object).GetFeeRatesAsync(cancellation.Token);
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
		fallback.VerifyNoOtherCalls();
	}
}
