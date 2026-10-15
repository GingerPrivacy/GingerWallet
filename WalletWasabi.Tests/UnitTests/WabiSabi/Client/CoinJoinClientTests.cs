using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinClientTests
{
	[Theory]
	[InlineData(6, 2, false, 1.00, true)]
	[InlineData(5, 2, false, 1.00, false)]
	[InlineData(6, 6, false, 1.00, false)]
	[InlineData(6, 6, true, 1.00, true)]
	[InlineData(6, 2, false, 0.89, false)]
	[InlineData(6, 2, false, 0.90, false)]
	[InlineData(6, 2, false, 0.91, true)]
	public void SigningChecksActualInputsAndMiningFee(int inputCount, int ownInputCount, bool allowSolo, decimal feeRatio, bool expected)
	{
		var parameters = WabiSabiTestFactory.CreateRoundParameters(WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		var inputs = Enumerable.Range(0, inputCount).Select(_ => WabiSabiTestFactory.CreateCoin()).ToArray();
		var output = new TxOut(Money.Zero, BitcoinFactory.CreateScript());
		var vsize = inputs.Sum(x => x.ScriptPubKey.EstimateInputVsize()) + output.ScriptPubKey.EstimateOutputVsize();
		output.Value = inputs.Sum(x => x.Amount) - Money.Satoshis((long)(parameters.MiningFeeRate.GetFee(vsize).Satoshi * feeRatio));
		var state = new SigningState(parameters, new IEvent[] { new RoundCreated(parameters), new OutputAdded(output) }
			.Concat(inputs.Select(x => new InputAdded(x, null!))));
		var configuration = new CoinJoinConfiguration("test", 0.3m, 150m, 6, allowSolo);

		Assert.Equal(expected, CoinJoinClient.IsSigningStateSafe(state, ownInputCount, configuration));
	}

	[Fact]
	public void SanityCheckTest()
	{
		var output1 = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript());
		var output2 = new TxOut(Money.Coins(2), BitcoinFactory.CreateScript());
		var output3 = new TxOut(Money.Coins(3), BitcoinFactory.CreateScript());
		var output4 = new TxOut(Money.Coins(4), BitcoinFactory.CreateScript());

		// Exact match (one expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output1 },
			new[] { output1, output2, output3, output4 }));

		// Exact match (two expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output3, output4 }));

		// Missing output
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output4 }));

		static TxOut AddSats(long sats, TxOut output) => new(output.Value + sats, output.ScriptPubKey);
		static TxOut AddOneSat(TxOut output) => AddSats(1, output);
		static TxOut SubOneSat(TxOut output) => AddSats(-1, output);

		// More money in one output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), output3, output4 }));

		// More money in all output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), AddOneSat(output3), output4 }));

		// Same scriptpubkeys, same amount of money but outputs were manipulated
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), SubOneSat(output3), output4 }));
	}

	[Fact]
	public void GetTxOutsTest()
	{
		FeeRate feeRate = new(10m);

		var outputs = new[]
		{
			Output.FromDenomination(Money.Coins(1m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(2m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(3m), ScriptType.Taproot, feeRate),
			Output.FromDenomination(Money.Coins(4m), ScriptType.Taproot, feeRate),
		};

		var password = "satoshi";
		var km = ServiceFactory.CreateKeyManager(password, true);
		var destinationProvider = new InternalDestinationProvider(km);

		var txOuts = OutputProvider.GetTxOuts(outputs, destinationProvider);

		// All the outputs were generated.
		Assert.Equal(txOuts.Count(), outputs.Length);

		// No address reuse.
		Assert.Distinct(txOuts.Select(x => x.ScriptPubKey));

		// Verify if all the outputs are generated with correct ScriptType and Value.
		List<TxOut> toCheck = txOuts.ToList();
		foreach (var output in outputs)
		{
			var foundTxOut = toCheck.First(txout => txout.ScriptPubKey.IsScriptType(output.ScriptType) && txout.Value == output.Amount);
			toCheck.Remove(foundTxOut);
		}

		Assert.Empty(toCheck);
	}
}
