using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class SubSatoshiFeeTests
{
	[Theory]
	[InlineData(0.1, false, false)]
	[InlineData(0.101, true, false)]
	[InlineData(0.125, false, true)]
	[InlineData(0.333, true, true)]
	[InlineData(0.999, false, false)]
	public void SignedAndWatchOnlyTransactionsCoverFractionalFees(decimal rate, bool subtractFee, bool watchOnly)
	{
		var factory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), [("Alice", 0, 0.01m, true, 1)], watchOnly);
		using var key = new Key();
		var destination = key.PubKey.GetAddress(ScriptPubKeyType.TaprootBIP86, Network.Main);
		var amount = Money.Coins(0.005m);
		var payment = new PaymentIntent(destination, MoneyRequest.Create(amount, subtractFee));
		var parameters = TransactionParametersBuilder.CreateDefault().SetPayment(payment).SetFeeRate(rate).Build();

		var result = factory.BuildTransaction(parameters);

		Assert.True(result.Psbt.TryGetVirtualSize(out var size));
		Assert.True(result.Fee.Satoshi >= Math.Ceiling(rate * size));
		Assert.Equal(amount - (subtractFee ? result.Fee : Money.Zero), Assert.Single(result.OuterWalletOutputs).Amount);
		Assert.Equal(result.SpentCoins.Sum(x => x.Amount.Satoshi) - result.Transaction.Transaction.Outputs.Sum(x => x.Value.Satoshi), result.Fee.Satoshi);
		Assert.Equal(!watchOnly, result.Signed);
		if (!watchOnly)
		{
			Assert.True(result.Psbt.IsAllFinalized());
		}
	}

	[Fact]
	public void MinimumStrategyRejectsBelowRelayFloor()
	{
		Assert.True(FeeStrategy.CreateFromFeeRate(0.1m).TryGetFeeRate(out var accepted));
		Assert.Equal(0.1m, accepted!.SatoshiPerByte);
		Assert.Throws<ArgumentOutOfRangeException>(() => FeeStrategy.CreateFromFeeRate(0.099m));
	}
}
