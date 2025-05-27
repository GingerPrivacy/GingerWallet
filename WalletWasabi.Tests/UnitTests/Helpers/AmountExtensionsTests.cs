using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class AmountExtensionsTests
{
	[Fact]
	public void DifferenceShouldBeExpected()
	{
		var previous = new Amount(Money.FromUnit(221, MoneyUnit.Satoshi).ToDecimal(MoneyUnit.BTC));
		var current = new Amount(Money.FromUnit(110, MoneyUnit.Satoshi).ToDecimal(MoneyUnit.BTC));

		var result = current.Diff(previous);

		var expected = -0.5m;
		decimal tolerance = 0.01m;
		var areApproximatelyEqual = Math.Abs((decimal)result - expected) < tolerance;
		Assert.True(areApproximatelyEqual, $"Result is not the expected by the given tolerance. Result: {result}, Expected: {expected}, Tolerance: {tolerance}");
	}
}
