using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets.SilentPayment;
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.WabiSabi.Client.Batching;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class SilentPaymentTransactionTests
{
	[Theory]
	[InlineData(ScriptPubKeyType.Segwit, true)]
	[InlineData(ScriptPubKeyType.TaprootBIP86, true)]
	[InlineData(ScriptPubKeyType.Segwit, false)]
	[InlineData(ScriptPubKeyType.TaprootBIP86, false)]
	public async Task SelectedInputKeysProduceRecoverableOutputsIncludingUnsignedPreviews(ScriptPubKeyType inputType, bool sign)
	{
		var manager = KeyManager.CreateNew(out _, "password", Network.Main);
		var receiveKey = manager.GetNextReceiveKey(LabelsArray.Empty, inputType);
		var coin = BitcoinFactory.CreateSmartCoin(TestRandom.Get(), receiveKey, Money.Coins(1));
		var factory = new TransactionFactory(Network.Main, manager, new CoinsView([coin]), new CoinTransactionStore(coin), "password");
		using var scanKey = ECPrivKey.Create(Encoders.Hex.DecodeData("7e3a1d4b5f8e2c9a0f6b4d3e9a1f0c8b7e5d2a3c4f6e8b7a9d0c1f2e3a4b5d61"));
		using var spendKey = ECPrivKey.Create(Encoders.Hex.DecodeData("3f9a7d2c6b8e1a4f5d0c2e7b9f3a6d4c1b0e8f7a9d5c2b4e7f6a3c9b0d1f8e22"));
		var address = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());
		var parameters = TransactionParametersBuilder.CreateDefault().SetPayment(new PaymentIntent(address, Money.Coins(0.9m)))
			.SetFeeRate(2m).SetTryToSign(sign).Build();
		var result = factory.BuildTransaction(parameters);
		var payment = Assert.Single(result.OuterWalletOutputs);
		Assert.Equal(Money.Coins(0.9m), payment.Amount);
		Assert.NotEqual(address.ScriptPubKey, payment.ScriptPubKey);
		Assert.DoesNotContain(result.Psbt.Outputs, o => o.ScriptPubKey == address.ScriptPubKey);
		Assert.Equal(sign, result.Psbt.IsAllFinalized());
		Assert.True(result.Transaction.IsSilentPayment);
		Assert.False(result.Transaction.IsRbfable(manager));
		Assert.True(result.Transaction.IsCpfpable(manager));
		Assert.False(result.Transaction.IsCancellable(manager));
		var exported = PSBT.Load(result.Psbt.ToBytes(), Network.Main);
		Assert.True(SilentPaymentTransaction.HasSilentPaymentOutputs(exported));
		if (!sign)
		{
			exported.SignWithKeys(manager.GetSecrets("password", coin.ScriptPubKey).Select(s => s.PrivateKey).ToArray());
			exported.Finalize();
		}
		Assert.True(exported.ExtractSmartTransaction().IsSilentPayment);
		Assert.True(exported.ExtractSmartTransaction(result.Transaction).IsSilentPayment);
		var path = Path.Combine(TestDirectory.Get(), "silent.psbt");
		await File.WriteAllBytesAsync(path, exported.ToBytes());
		Assert.True((await TransactionHelpers.ParseTransactionAsync(path, Network.Main)).IsSilentPayment);

		var inputPubKey = inputType == ScriptPubKeyType.TaprootBIP86
			? ECXOnlyPubKey.Create(PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(coin.ScriptPubKey)!.ToBytes()).Q
			: ECPubKey.Create(receiveKey.PubKey.ToBytes()).Q;
		var shared = SilentPayment.ComputeSharedSecretReceiver([coin.Outpoint], [inputPubKey], scanKey);
		using var paymentKey = SilentPayment.ComputePrivKey(spendKey, shared, 0);
		Assert.Equal(new TaprootPubKey(paymentKey.CreateXOnlyPubKey().ToBytes()).ScriptPubKey, payment.ScriptPubKey);
		var normalParameters = TransactionParametersBuilder.CreateDefault()
			.SetPayment(new PaymentIntent(BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main), Money.Coins(0.9m)))
			.SetFeeRate(2m).SetTryToSign(sign).Build();
		var normal = factory.BuildTransaction(normalParameters).Transaction;
		Assert.False(normal.IsSilentPayment);
		Assert.True(normal.IsRbfable(manager));
		var change = Assert.Single(result.InnerWalletOutputs);
		var childFactory = new TransactionFactory(Network.Main, manager, new CoinsView([change]), new CoinTransactionStore(change), "password");
		var childParameters = TransactionParametersBuilder.CreateDefault().SetPayment(new PaymentIntent(
			BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main), Money.Coins(0.05m)))
			.SetFeeRate(2m).SetTryToSign(sign).SetAllowUnconfirmed(true).Build();
		var child = childFactory.BuildTransaction(childParameters).Transaction;
		Assert.Contains(child.Transaction.Inputs, i => i.PrevOut.Hash == result.Transaction.GetHash());
		Assert.False(child.IsSilentPayment);
		Assert.True(child.IsRbfable(manager));
	}

	[Fact]
	public void MixedInputsAndRecipientsResolveDistinctOutputsForOneScanKey()
	{
		var manager = KeyManager.CreateNew(out _, "password", Network.Main);
		var segwit = manager.GetNextReceiveKey(LabelsArray.Empty, ScriptPubKeyType.Segwit);
		var taproot = manager.GetNextReceiveKey(LabelsArray.Empty, ScriptPubKeyType.TaprootBIP86);
		var coins = new[] { segwit, taproot }.Select(k => BitcoinFactory.CreateSmartCoin(TestRandom.Get(), k, Money.Coins(1))).ToArray();
		var factory = new TransactionFactory(Network.Main, manager, new CoinsView(coins), new CoinTransactionStore(coins), "password");
		using var scan = ECPrivKey.Create(Encoders.Hex.DecodeData("7e3a1d4b5f8e2c9a0f6b4d3e9a1f0c8b7e5d2a3c4f6e8b7a9d0c1f2e3a4b5d61"));
		using var spend1 = ECPrivKey.Create(Encoders.Hex.DecodeData("3f9a7d2c6b8e1a4f5d0c2e7b9f3a6d4c1b0e8f7a9d5c2b4e7f6a3c9b0d1f8e22"));
		// Opposite spend-key parity must not collide in the fee-estimation placeholders.
		using var spend2 = ECPrivKey.Create(spend1.sec.Negate().ToBytes());
		var addresses = new[] { new SilentPaymentAddress(0, scan.CreatePubKey(), spend1.CreatePubKey()), new SilentPaymentAddress(0, scan.CreatePubKey(), spend2.CreatePubKey()) };
		var ordinary = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
		var payment = new PaymentIntent([
			new DestinationRequest(addresses[0], MoneyRequest.Create(Money.Coins(0.6m))),
			new DestinationRequest(addresses[1], MoneyRequest.Create(Money.Coins(0.7m))),
			new DestinationRequest(ordinary, MoneyRequest.Create(Money.Coins(0.2m)))]);
		var result = factory.BuildTransaction(TransactionParametersBuilder.CreateDefault().SetPayment(payment).SetFeeRate(2m).Build());
		Assert.Equal(2, result.SpentCoins.Count());
		Assert.Equal(3, result.OuterWalletOutputs.Count());
		Assert.Contains(result.OuterWalletOutputs, c => c.ScriptPubKey == ordinary.ScriptPubKey && c.Amount == Money.Coins(0.2m));
		var inputPubs = new[] { ECPubKey.Create(segwit.PubKey.ToBytes()).Q, ECXOnlyPubKey.Create(PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(coins[1].ScriptPubKey)!.ToBytes()).Q };
		var shared = SilentPayment.ComputeSharedSecretReceiver(coins.Select(c => c.Outpoint).ToArray(), inputPubs, scan);
		var spends = new[] { spend1, spend2 };
		for (uint k = 0; k < 2; k++)
		{
			using var recoveredKey = SilentPayment.ComputePrivKey(spends[k], shared, k);
			var expected = new TaprootPubKey(recoveredKey.CreateXOnlyPubKey().ToBytes()).ScriptPubKey;
			Assert.Contains(result.OuterWalletOutputs, c => c.ScriptPubKey == expected && c.Amount == Money.Coins(k == 0 ? 0.6m : 0.7m));
			Assert.DoesNotContain(result.Psbt.Outputs, o => o.ScriptPubKey == addresses[k].ScriptPubKey);
		}
	}

	[Fact]
	public void RejectsWatchOnlyPayjoinAndWrongNetworkBeforeBuilding()
	{
		var factory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), [("sender", 0, 1m, true, 1)]);
		var watchOnly = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), [("sender", 0, 1m, true, 1)], watchOnly: true);
		var address = SilentPaymentAddress.Parse("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", Network.Main);
		var parameters = TransactionParametersBuilder.CreateDefault().SetPayment(new PaymentIntent(address, Money.Coins(0.1m))).SetFeeRate(2m).Build();
		Assert.Throws<InvalidOperationException>(() => watchOnly.BuildTransaction(parameters));
		Assert.Throws<InvalidOperationException>(() => factory.BuildTransaction(parameters, payjoinClient: new UnsupportedPayjoin()));
		var wrongNetwork = TransactionParametersBuilder.CreateDefault().SetPayment(new PaymentIntent(address with { Network = Network.TestNet }, Money.Coins(0.1m))).SetFeeRate(2m).Build();
		Assert.Throws<ArgumentException>(() => factory.BuildTransaction(wrongNetwork));
		var hot = KeyManager.CreateNew(out _, "", Network.Main);
		var hardware = KeyManager.CreateNewHardwareWalletWatchOnly(hot.MasterFingerprint!.Value, hot.SegwitExtPubKey, hot.TaprootExtPubKey, Network.Main);
		var hardwareFactory = new TransactionFactory(Network.Main, hardware, new CoinsView([]), new EmptyTransactionStore(Network.Main));
		Assert.Throws<InvalidOperationException>(() => hardwareFactory.BuildTransaction(parameters));
		var batch = new PaymentBatch();
		Assert.Throws<InvalidOperationException>(() => batch.AddPayment(address, Money.Coins(0.1m)));
		Assert.Empty(batch.GetPayments());
	}

	[Fact]
	public void ParsesSilentPaymentUriAndRejectsBadChecksumAndOtherNetworks()
	{
		const string text = "sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3";
		Assert.True(AddressStringParser.TryParse($"bitcoin:{text}?amount=0.1&label=recipient", Network.Main, out var result));
		Assert.IsType<SilentPaymentAddress>(result!.Address);
		Assert.Equal(Money.Coins(0.1m), result.Amount);
		Assert.Equal("recipient", result.Label);
		Assert.False(AddressStringParser.TryParse(text, Network.TestNet, out _));
		Assert.False(AddressStringParser.TryParse(text[..^1] + "q", Network.Main, out _));
	}

	private class CoinTransactionStore(params SmartCoin[] coins) : ITransactionStore
	{
		public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? transaction)
		{
			transaction = coins.FirstOrDefault(c => hash == c.TransactionId)?.Transaction;
			return transaction is not null;
		}
	}

	private class UnsupportedPayjoin : IPayjoinClient
	{
		public Uri PaymentUrl => new("https://example.com/payjoin");
		public Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, HdPubKey changeHdPubKey, CancellationToken cancellationToken) => throw new InvalidOperationException("Payjoin must not be called.");
		public Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, IHDKey? taprootAccountKey, RootedKeyPath taprootRootedKeyPath, HdPubKey? changeHdPubKey, CancellationToken cancellationToken) => throw new InvalidOperationException("Payjoin must not be called.");
	}
}
