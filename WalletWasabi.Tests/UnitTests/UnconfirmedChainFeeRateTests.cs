using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class UnconfirmedChainFeeRateTests
{
	// Size and Fee are taken from mainnet CJ ID: 1023f9a6109aa2acad60de87df0ed03885f9a1c1e72ca92ff20bb88b7353b5ef
	[Fact]
	public void CanCalculateEffectiveFeeRateSingleTransaction()
	{
		var expectedEffectiveFeeRate = new FeeRate(30.412m);

		var unconfirmedChain = new List<UnconfirmedTransactionChainItem>
		{
			new(uint256.One, Size: 20750, Fee: new Money((long) 631051), Parents: new(), Children: new())
		};

		var effectiveFeeRate = FeeHelpers.CalculateEffectiveFeeRateOfUnconfirmedChain(unconfirmedChain);

		Assert.Equal(expectedEffectiveFeeRate, effectiveFeeRate);
	}

	[Fact]
	public void CanCalculateEffectiveFeeRateSameSizeTransactions()
	{
		int size = 100;
		var expectedEffectiveFeeRate = new FeeRate(15m);

		var unconfirmedChain = new List<UnconfirmedTransactionChainItem>
		{
			new(uint256.Zero, size, Fee: new Money((long)1000), Parents: new(), Children: new() { uint256.One }),
			new(uint256.One, size, Fee: new Money((long)2000), Parents: new() { uint256.Zero }, Children: new())
		};

		var effectiveFeeRate = FeeHelpers.CalculateEffectiveFeeRateOfUnconfirmedChain(unconfirmedChain);

		Assert.Equal(expectedEffectiveFeeRate, effectiveFeeRate);
	}

	// Sizes and Fees are taken from Regtest.
	[Fact]
	public void CanCalculateEffectiveFeeRate()
	{
		var expectedEffectiveFeeRate = new FeeRate(147.783m);

		var unconfirmedChain = new List<UnconfirmedTransactionChainItem>
		{
			new(uint256.Zero, Size: 1064, Fee: new Money((long) 150215), Parents: new(), Children: new() { uint256.One }),
			new(uint256.One, Size: 272, Fee: new Money((long) 43680), Parents: new() { uint256.Zero }, Children: new() { new uint256(2) }),
			new(new uint256(2), Size: 110, Fee: new Money((long) 19800), Parents: new() { uint256.One }, Children: new())
		};

		var effectiveFeeRate = FeeHelpers.CalculateEffectiveFeeRateOfUnconfirmedChain(unconfirmedChain);

		Assert.Equal(expectedEffectiveFeeRate, effectiveFeeRate);
	}

	[Fact]
	public async Task CanCalculateTransactionFeeFromConfirmedPrevoutAsync()
	{
		var parent = CreateTransaction(Money.Satoshis(10_000));
		var transaction = CreateSpendingTransaction(new OutPoint(parent, 0), Money.Satoshis(9_000));

		var fee = await UnconfirmedTransactionFeeResolver.ComputeFeeAsync(
			transaction,
			mempoolParentTransactions: [],
			getConfirmedTxOutAsync: (prevOut, _) => Task.FromResult<TxOut?>(parent.Outputs[prevOut.N]),
			CancellationToken.None);

		Assert.Equal(Money.Satoshis(1_000), fee);
	}

	[Fact]
	public async Task CanCalculateTransactionFeeFromMempoolParentAsync()
	{
		var parent = CreateTransaction(Money.Satoshis(8_000));
		var transaction = CreateSpendingTransaction(new OutPoint(parent, 0), Money.Satoshis(7_500));

		var fee = await UnconfirmedTransactionFeeResolver.ComputeFeeAsync(
			transaction,
			mempoolParentTransactions: [parent],
			getConfirmedTxOutAsync: (_, _) => throw new InvalidOperationException("Mempool parent should not require gettxout."),
			CancellationToken.None);

		Assert.Equal(Money.Satoshis(500), fee);
	}

	[Fact]
	public async Task ThrowsWhenPrevoutCannotBeResolvedAsync()
	{
		var parent = CreateTransaction(Money.Satoshis(8_000));
		var transaction = CreateSpendingTransaction(new OutPoint(parent, 0), Money.Satoshis(7_500));

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await UnconfirmedTransactionFeeResolver.ComputeFeeAsync(
				transaction,
				mempoolParentTransactions: [],
				getConfirmedTxOutAsync: (_, _) => Task.FromResult<TxOut?>(null),
				CancellationToken.None));

		Assert.Contains("Failed to resolve prevout", ex.Message);
	}

	private static Transaction CreateTransaction(Money outputAmount)
	{
		var transaction = Transaction.Create(Network.RegTest);
		transaction.Inputs.Add(new OutPoint(uint256.One, 0));
		using var key = new Key();
		transaction.Outputs.Add(outputAmount, key.PubKey.WitHash.ScriptPubKey);
		return transaction;
	}

	private static Transaction CreateSpendingTransaction(OutPoint prevOut, Money outputAmount)
	{
		var transaction = Transaction.Create(Network.RegTest);
		transaction.Inputs.Add(prevOut);
		using var key = new Key();
		transaction.Outputs.Add(outputAmount, key.PubKey.WitHash.ScriptPubKey);
		return transaction;
	}
}
