using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class BatchPaymentTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void BatchPaymentsPreserveEveryOutputAndSubtractOnlyFromFirst(bool subtractFee, bool watchOnly)
	{
		var factory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(),
			new[] { ("Savings", 0, 0.1m, true, 50) }, watchOnly);
		using Key firstKey = new();
		using Key secondKey = new();
		var first = firstKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
		var second = secondKey.PubKey.GetAddress(ScriptPubKeyType.TaprootBIP86, Network.Main);
		var info = new TransactionInfo(first, 50)
		{
			Amount = Money.Coins(subtractFee ? 0.06m : 0.02m),
			Recipient = "Alice",
			AdditionalRecipients = new[] { new RecipientInfo(second, Money.Coins(0.04m), "Bob") },
			SubtractFee = subtractFee,
			FeeRate = new FeeRate(2m)
		};

		var clone = info.Clone();
		Assert.Equal(info.TotalAmount, clone.TotalAmount);
		Assert.Equal(new LabelsArray("Alice", "Bob"), clone.AllRecipientLabels);
		var intent = TransactionHelpers.BuildPaymentIntent(clone);
		Assert.Equal(subtractFee ? 1 : 0, intent.Requests.Count(x => x.Amount.SubtractFee));
		var parameters = TransactionParametersBuilder.CreateDefault().SetFeeRate(2m).SetPayment(intent).Build();
		var result = factory.BuildTransaction(parameters);
		Assert.Equal(!watchOnly, result.Signed);
		Assert.Equal(Money.Coins(0.04m), result.Transaction.Transaction.Outputs.Single(x => x.ScriptPubKey == second.ScriptPubKey).Value);
		Assert.Equal(info.Amount - (subtractFee ? result.Fee : Money.Zero),
			result.Transaction.Transaction.Outputs.Single(x => x.ScriptPubKey == first.ScriptPubKey).Value);
		Assert.Equal(info.Amount - (subtractFee ? result.Fee : Money.Zero), result.CalculatePaymentAmount(first));
		Assert.Equal(Money.Coins(0.04m), result.CalculatePaymentAmount(second));
		Assert.Equal(info.TotalAmount - (subtractFee ? result.Fee : Money.Zero), info.AllRecipients.Select(x => result.CalculatePaymentAmount(x.Destination)).Sum());
		Assert.Equal(result.Transaction.Transaction.Outputs.Select(x => (x.ScriptPubKey, x.Value)),
			result.Psbt.GetGlobalTransaction().Outputs.Select(x => (x.ScriptPubKey, x.Value)));
	}

	[Fact]
	public void BatchPreviewAmountsIncludeInternalPaymentsAndExcludeChange()
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var selfPayment = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var change = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), 0,
			new[] { Money.Coins(0.1m) }, new[] { (Money.Coins(1m), 20, input) },
			new[] { (Money.Coins(0.2m), 20, selfPayment), (Money.Coins(0.69m), 20, change) });
		var result = new BuildTransactionResult(transaction, PSBT.FromTransaction(transaction.Transaction, Network.Main), false, Money.Coins(0.01m), 0, new());
		var external = Assert.Single(result.OuterWalletOutputs).ScriptPubKey.GetDestinationAddress(Network.Main)!;
		var info = new TransactionInfo(selfPayment.P2wpkhScript.GetDestinationAddress(Network.Main)!, 20)
		{
			Amount = Money.Coins(0.2m),
			AdditionalRecipients = new[] { new RecipientInfo(external, Money.Coins(0.1m), "Bob") }
		};
		Assert.Equal(Money.Coins(0.2m), result.CalculatePaymentAmount(info.Destination));
		Assert.Equal(Money.Coins(0.1m), result.CalculatePaymentAmount(external));
		Assert.Equal(info.TotalAmount, info.AllRecipients.Select(x => result.CalculatePaymentAmount(x.Destination)).Sum());
		using Key originalPayjoinDestination = new();
		Assert.Equal(Money.Coins(0.1m), result.CalculateDestinationAmount(originalPayjoinDestination.PubKey.WitHash));
	}

	[Fact]
	public void ClonePreservesGeneralDestinationsWhileBatchInputRemainsAddressOnly()
	{
		using Key key = new();
		var address = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
		IDestination destination = key.PubKey.WitHash;
		var info = new TransactionInfo(address, 20)
		{
			Amount = Money.Satoshis(1),
			AdditionalRecipients = new[] { new RecipientInfo(destination, Money.Satoshis(2), LabelsArray.Empty) }
		};
		var clone = info.Clone();
		Assert.Same(destination, clone.AllRecipients.Last().Destination);
		Assert.Equal(Money.Satoshis(3), clone.TotalAmount);
		Assert.True(RecipientInfo.TryCreate(address.ToString(), 0.00000001m, LabelsArray.Empty, Network.Main, out var parsed, out _));
		Assert.IsAssignableFrom<BitcoinAddress>(parsed!.Destination);
	}

	[Theory]
	[InlineData(0.000000001)]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(21000001)]
	public void RejectsInvalidAmounts(double amount)
	{
		using Key key = new();
		Assert.False(RecipientInfo.TryCreate(key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString(),
			(decimal)amount, LabelsArray.Empty, Network.Main, out _, out _));
	}

	[Fact]
	public void BatchRecipientRejectsPaymentUriAndWrongNetwork()
	{
		using Key key = new();
		var address = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();
		Assert.False(RecipientInfo.TryCreate($"bitcoin:{address}?amount=1", 1m, LabelsArray.Empty, Network.Main, out _, out _));
		Assert.False(RecipientInfo.TryCreate(address, 1m, LabelsArray.Empty, Network.TestNet, out _, out _));
		Assert.True(RecipientInfo.TryCreate(address, 0.00000001m, "Alice", Network.Main, out var recipient, out _));
		Assert.Equal(Money.Satoshis(1), recipient!.Amount);
	}
}
