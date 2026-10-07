using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class TransactionCoinsTests
{
	[Fact]
	public void PreservesAllRowsAndUnknownForeignInputAmounts()
	{
		var random = TestRandom.Get();
		var keys = ServiceFactory.CreateKeyManager();
		var inputKey = BitcoinFactory.CreateHdPubKey(keys);
		var changeKey = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var transaction = BitcoinFactory.CreateSmartTransaction(random, 2,
			new[] { Money.Coins(0.1m), Money.Coins(0.2m) },
			new[] { (Money.Coins(1m), 20, inputKey) },
			new[] { (Money.Coins(0.69m), 20, changeKey) });
		var lists = new TransactionCoinsModel(transaction, Network.Main, script => keys.TryGetKeyForScriptPubKey(script, out _));
		Assert.Equal(transaction.Transaction.Inputs.Select(x => x.PrevOut), lists.Inputs.Select(x => x.OutPoint));
		Assert.Equal(3, lists.Inputs.Count);
		Assert.Equal(2, lists.Inputs.Count(x => x.Amount is null));
		Assert.Equal(Money.Coins(1m), Assert.Single(lists.Inputs, x => x.IsOwn).Amount);
		Assert.Equal(transaction.Transaction.Outputs.Select(x => x.Value), lists.Outputs.Select(x => x.Amount));
		Assert.True(Assert.Single(lists.Outputs, x => x.IsOwn).IsChange);

		var selfPayment = new TransactionCoinsModel(transaction, Network.Main,
			script => keys.TryGetKeyForScriptPubKey(script, out _), new[] { changeKey.P2wpkhScript });
		Assert.False(Assert.Single(selfPayment.Outputs, x => x.IsOwn).IsChange);
	}

	[Fact]
	public void OtherLoadedWalletsAreNotMarkedAsThisWallet()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), ownInputCount: 1, ownOutputCount: 1);
		var lists = new TransactionCoinsModel(transaction, Network.Main, _ => false);
		Assert.DoesNotContain(lists.Inputs, x => x.IsOwn);
		Assert.DoesNotContain(lists.Outputs, x => x.IsOwn || x.IsChange);
		Assert.All(lists.Inputs, x => Assert.Empty(x.Labels));
		Assert.All(lists.Outputs, x => Assert.Empty(x.Labels));
	}

	[Fact]
	public void NonAddressOutputsShowTheirScriptInsteadOfDroppingTheRow()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get());
		var script = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2, 3 });
		transaction.Transaction.Outputs.Add(Money.Zero, script);
		var lists = new TransactionCoinsModel(transaction, Network.Main, _ => false);
		Assert.Equal(script.ToHex(), lists.Outputs.Last().Address);
		Assert.Equal(Money.Zero, lists.Outputs.Last().Amount);
	}
}
