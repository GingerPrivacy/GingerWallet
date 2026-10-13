using System.IO;
using System.Linq;
using System.Text.Json;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinCostsTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SeparatesCoordinationFeesAndHonorsFeeExemptions(bool exempt)
	{
		using Key inputKey = new();
		using Key outputKey = new();
		var input = new Coin(new OutPoint(uint256.One, 0), new TxOut(Money.Coins(1m), inputKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)));
		var outputScript = outputKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);
		var miningRate = new FeeRate(2.5m);
		var coordinationRate = new CoordinationFeeRate(0.003m, Money.Coins(0.03m));
		var mining = miningRate.GetFee(input.ScriptPubKey.EstimateInputVsize()) + miningRate.GetFee(outputScript.EstimateOutputVsize());
		var coordination = exempt ? Money.Zero : Money.Coins(0.003m);
		var dust = Money.Satoshis(123);
		var output = new TxOut(input.Amount - mining - coordination - dust, outputScript);
		var costs = CoinJoinCosts.Calculate(miningRate, coordinationRate, new[] { (input, exempt) }, new[] { output });
		Assert.Equal(new CoinJoinCosts(mining, coordination, dust, Money.Zero), costs);
		Assert.Equal(input.Amount - output.Value, costs!.TotalFee);
	}

	[Fact]
	public void ThresholdFeesAndDifferentInputTypesUseActualProtocolRounding()
	{
		using Key key = new();
		var low = new Coin(new OutPoint(uint256.One, 0), new TxOut(Money.Coins(0.01m), key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)));
		var high = new Coin(new OutPoint(uint256.One, 1), new TxOut(Money.Coins(0.5m), key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86)));
		var miningRate = new FeeRate(1.25m);
		var coordinationRate = new CoordinationFeeRate(0.003m, Money.Coins(0.03m));
		var outputScript = key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		var mining = new[] { low, high }.Sum(x => miningRate.GetFee(x.ScriptPubKey.EstimateInputVsize())) + miningRate.GetFee(outputScript.EstimateOutputVsize());
		var output = new TxOut(low.Amount + high.Amount - mining - coordinationRate.GetFee(high.Amount), outputScript);
		var costs = CoinJoinCosts.Calculate(miningRate, coordinationRate, new[] { (low, false), (high, false) }, new[] { output });
		Assert.Equal(coordinationRate.GetFee(high.Amount), costs!.CoordinationFee);
		Assert.Equal(mining, costs.MiningFee);
		Assert.Equal(Money.Zero, costs.WastedDust);
	}

	[Fact]
	public void InvalidAccountingStaysUnknownInsteadOfShowingNegativeFees()
	{
		using Key key = new();
		var input = new Coin(new OutPoint(uint256.One, 0), new TxOut(Money.Coins(1m), key));
		Assert.Null(CoinJoinCosts.Calculate(new FeeRate(2m), CoordinationFeeRate.Zero,
			new[] { (input, false) }, new[] { new TxOut(Money.Coins(2m), key) }));
		Assert.Null(CoinJoinCosts.Calculate(FeeRate.Zero, CoordinationFeeRate.Zero,
			Array.Empty<(Coin, bool)>(), new[] { new TxOut(Money.Coins(1m), key) }));
	}

	[Fact]
	public void AggregationRequiresCompleteHistoryAndKeepsPaymentsSeparate()
	{
		var first = new CoinJoinCosts(Money.Satoshis(10), Money.Satoshis(20), Money.Satoshis(3), Money.Coins(0.1m));
		var second = new CoinJoinCosts(Money.Satoshis(11), Money.Zero, Money.Satoshis(4), Money.Coins(0.2m));
		var total = CoinJoinCosts.Aggregate(new[] { first, second });
		Assert.Equal(Money.Satoshis(48), total!.TotalFee);
		Assert.Equal(Money.Coins(0.3m), total.PaymentsTotal);
		Assert.Null(CoinJoinCosts.Aggregate(new CoinJoinCosts?[] { first, null }));
		Assert.Null(CoinJoinCosts.Aggregate(Array.Empty<CoinJoinCosts>()));
	}

	[Fact]
	public void UsesActualRegisteredOutputsAndRejectsMissingOutputs()
	{
		using Key key = new();
		using Key otherKey = new();
		var input = new Coin(new OutPoint(uint256.One, 0), new TxOut(Money.Coins(1m), key));
		var expected = new TxOut(Money.Coins(0.9m), key);
		var actual = new TxOut(Money.Coins(0.95m), key);
		var unrelated = new TxOut(Money.Coins(2m), otherKey);
		var costs = CoinJoinCosts.Calculate(FeeRate.Zero, CoordinationFeeRate.Zero,
			new[] { (input, false) }, new[] { expected }, new[] { actual, unrelated });
		Assert.Equal(Money.Satoshis(2), costs!.MiningFee);
		Assert.Equal(Money.Coins(0.05m) - Money.Satoshis(2), costs.WastedDust);
		Assert.Null(CoinJoinCosts.Calculate(FeeRate.Zero, CoordinationFeeRate.Zero,
			new[] { (input, false) }, new[] { expected }, new[] { unrelated }));
	}

	[Fact]
	public void PaymentTotalUsesOnlyReservedOutputsFromTheFinalTransaction()
	{
		using Key paid = new();
		using Key pending = new();
		var batch = new PaymentBatch();
		batch.AddPayment(paid, Money.Coins(0.1m));
		batch.MovePaymentsToInProgress(batch.GetPayments().ToArray(), uint256.One);
		batch.AddPayment(pending, Money.Coins(0.2m));
		var transaction = Transaction.Create(Network.Main);
		transaction.Outputs.Add(Money.Coins(0.11m), paid);
		transaction.Outputs.Add(Money.Coins(0.2m), pending);
		Assert.Equal(Money.Coins(0.11m), batch.GetInProgressPaymentsTotal(transaction));
		batch.MovePaymentsToFinished(transaction.GetHash());
		Assert.Equal(Money.Zero, batch.GetInProgressPaymentsTotal(transaction));
	}

	[Fact]
	public void CostsPersistAcrossWalletReloads()
	{
		var filePath = Path.Combine(TestDirectory.Get(), "coinjoin-costs.json");
		var manager = KeyManager.CreateNew(out _, "", Network.Main, filePath);
		var costs = new CoinJoinCosts(Money.Satoshis(12), Money.Satoshis(34), Money.Satoshis(5), Money.Zero);
		manager.AddCoinJoinCosts(uint256.One, costs);
		var restored = KeyManager.FromFile(filePath);
		Assert.Equal(costs, restored.GetCoinJoinCosts(uint256.One));
		Assert.Null(restored.GetCoinJoinCosts(uint256.Zero));
		Assert.Throws<ArgumentException>(() => restored.AddCoinJoinCosts(uint256.Zero, costs with { MiningFee = Money.Satoshis(-1) }));
		Assert.Null(restored.GetCoinJoinCosts(uint256.Zero));
	}

	[Fact]
	public void WalletAttributesRoundTripAndOlderAttributesRemainCompatible()
	{
		var costs = new CoinJoinCosts(Money.Satoshis(12), Money.Satoshis(34), Money.Satoshis(5), Money.Coins(0.1m));
		var attributes = new WalletAttributes();
		attributes.CoinJoinCosts.Add(uint256.One.ToString(), costs);
		var json = JsonSerializer.Serialize(attributes, KeyManager.JsonOptions);
		var restored = JsonSerializer.Deserialize<WalletAttributes>(json, KeyManager.JsonOptions)!;
		Assert.Equal(costs, restored.CoinJoinCosts[uint256.One.ToString()]);
		Assert.Empty(JsonSerializer.Deserialize<WalletAttributes>("{}", KeyManager.JsonOptions)!.CoinJoinCosts);
	}
}
