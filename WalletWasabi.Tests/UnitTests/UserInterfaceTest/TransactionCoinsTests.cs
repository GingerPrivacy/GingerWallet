using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class TransactionCoinsTests
{
	[Theory]
	[InlineData(1.0, 1)]
	[InlineData(20.75, 20)]
	[InlineData(int.MaxValue, null)]
	public void ShowsKnownOwnScoresAndDoesNotPredictPreviewOutputScores(double score, int? expected)
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var output = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), 1,
			new[] { Money.Coins(0.1m) }, new[] { (Money.Coins(1m), 20, input) },
			new[] { (Money.Coins(0.89m), 20, output) });
		input.SetAnonymitySet(score);
		output.SetAnonymitySet(score);
		bool IsOwn(Script script) => keys.TryGetKeyForScriptPubKey(script, out _);
		var history = new TransactionCoinsModel(transaction, Network.Main, IsOwn);
		Assert.Equal(expected, Assert.Single(history.Inputs, x => x.IsOwn).AnonymityScore);
		Assert.Equal(expected, Assert.Single(history.Outputs, x => x.IsOwn).AnonymityScore);
		Assert.All(history.Inputs.Concat(history.Outputs).Where(x => !x.IsOwn), x => Assert.Null(x.AnonymityScore));
		var result = new BuildTransactionResult(transaction, PSBT.FromTransaction(transaction.Transaction, Network.Main),
			false, Money.Satoshis(1000), 0, new());
		var preview = new TransactionCoinsModel(result, Network.Main, IsOwn, new Dictionary<Script, LabelsArray>());
		Assert.Equal(expected, Assert.Single(preview.Inputs, x => x.IsOwn).AnonymityScore);
		Assert.All(preview.Outputs, x => Assert.Null(x.AnonymityScore));
	}

	[Fact]
	public void PreviewKeepsInternalBatchRecipientsAndResolvedExternalOutputsSeparateFromChange()
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var selfPayment = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var change = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		using Key unresolvedDestination = new();
		var unresolvedScript = unresolvedDestination.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), 0,
			new[] { Money.Coins(0.1m) }, new[] { (Money.Coins(1m), 20, input) },
			new[] { (Money.Coins(0.2m), 20, selfPayment), (Money.Coins(0.69m), 20, change) });
		var result = new BuildTransactionResult(transaction, PSBT.FromTransaction(transaction.Transaction, Network.Main),
			false, Money.Coins(0.01m), 0, new());
		var lists = new TransactionCoinsModel(result, Network.Main,
			script => keys.TryGetKeyForScriptPubKey(script, out _),
			new[] { selfPayment.P2wpkhScript, unresolvedScript }.ToDictionary(script => script, _ => LabelsArray.Empty));
		Assert.False(Assert.Single(lists.Outputs, x => x.Address == selfPayment.P2wpkhScript.GetDestinationAddress(Network.Main)!.ToString()).IsChange);
		Assert.True(Assert.Single(lists.Outputs, x => x.Address == change.P2wpkhScript.GetDestinationAddress(Network.Main)!.ToString()).IsChange);
		var external = Assert.Single(result.OuterWalletOutputs);
		var externalRow = Assert.Single(lists.Outputs, x => x.OutPoint == external.Outpoint);
		Assert.False(externalRow.IsOwn || externalRow.IsChange);
		Assert.Equal(external.Amount, externalRow.Amount);
		Assert.DoesNotContain(lists.Outputs, x => x.Address == unresolvedScript.GetDestinationAddress(Network.Main)!.ToString());
	}

	[Fact]
	public void PreviewShowsAllCurrentRecipientAndPendingChangeLabelsWithoutSavingThem()
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var recipient = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var change = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		input.SetLabel("Dávid");
		recipient.SetLabel("Dávid");
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), 0,
			new[] { Money.Coins(0.1m) }, new[] { (Money.Coins(1m), 20, input) },
			new[] { (Money.Coins(0.2m), 20, recipient), (Money.Coins(0.69m), 20, change) });
		var recipientLabels = new LabelsArray("Dávid", "Gabika");
		var externalLabels = new LabelsArray("Alice", "Bob");
		var changeLabels = LabelsArray.Merge(recipientLabels, externalLabels);
		var result = new BuildTransactionResult(transaction, PSBT.FromTransaction(transaction.Transaction, Network.Main),
			false, Money.Coins(0.01m), 0, new() { [recipient] = recipientLabels, [change] = changeLabels });
		var external = Assert.Single(result.OuterWalletOutputs);
		var lists = new TransactionCoinsModel(result, Network.Main, script => keys.TryGetKeyForScriptPubKey(script, out _),
			new Dictionary<Script, LabelsArray> { [recipient.P2wpkhScript] = recipientLabels, [external.ScriptPubKey] = externalLabels });
		Assert.Equal(recipientLabels, Assert.Single(lists.Outputs, x => x.ScriptPubKey == recipient.P2wpkhScript).Labels);
		Assert.Equal(externalLabels, Assert.Single(lists.Outputs, x => x.ScriptPubKey == external.ScriptPubKey).Labels);
		Assert.Equal(changeLabels, Assert.Single(lists.Outputs, x => x.IsChange).Labels);
		Assert.Equal(new LabelsArray("Dávid"), Assert.Single(lists.Inputs).Labels);
		Assert.Equal(new LabelsArray("Dávid"), recipient.Labels);
		Assert.Empty(change.Labels);
	}

	[Fact]
	public void PreviewDoesNotExposePendingLabelsFromAnotherWallet()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), ownInputCount: 1, ownOutputCount: 1);
		var coin = Assert.Single(transaction.WalletOutputs);
		var result = new BuildTransactionResult(transaction, PSBT.FromTransaction(transaction.Transaction, Network.Main),
			false, Money.Satoshis(1000), 0, new() { [coin.HdPubKey] = new LabelsArray("Other wallet") });
		var lists = new TransactionCoinsModel(result, Network.Main, _ => false, new Dictionary<Script, LabelsArray>());
		Assert.All(lists.Outputs, x => Assert.Empty(x.Labels));
	}

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
		Assert.False(Assert.Single(lists.Outputs, x => x.IsOwn).IsChange);

		var selfPayment = new TransactionCoinsModel(transaction, Network.Main,
			script => keys.TryGetKeyForScriptPubKey(script, out _), new[] { changeKey.P2wpkhScript });
		Assert.False(Assert.Single(selfPayment.Outputs, x => x.IsOwn).IsChange);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void HistoryDoesNotInferChangeFromInternalKeys(int foreignInputs)
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var output = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		var transaction = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), foreignInputs,
			Array.Empty<Money>(), new[] { (Money.Coins(1m), 20, input) },
			new[] { (Money.Coins(0.99m), 20, output) });
		var lists = new TransactionCoinsModel(transaction, Network.Main,
			script => keys.TryGetKeyForScriptPubKey(script, out _));
		var row = Assert.Single(lists.Outputs);
		Assert.True(row.IsOwn);
		Assert.False(row.IsChange);
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
		Assert.All(lists.Inputs.Concat(lists.Outputs), x => Assert.Null(x.AnonymityScore));
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
