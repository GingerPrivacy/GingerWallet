using NBitcoin;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.PayJoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class PayjoinTests
{
	private static ICoin Coin(decimal amount, Script scriptPubKey)
	{
		return new Coin(GetRandomOutPoint(), new TxOut(Money.Coins(amount), scriptPubKey));
	}

	private static OutPoint GetRandomOutPoint()
	{
		return new OutPoint(RandomUtils.GetUInt256(), 0);
	}

	private static async Task<HttpResponseMessage> PayjoinServerOkAsync(HttpRequestMessage request, Func<PSBT, PSBT> transformPsbt, HttpStatusCode statusCode = HttpStatusCode.OK)
	{
		var body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
		var psbt = PSBT.Parse(body, Network.Main);
		var newPsbt = transformPsbt(psbt);
		var message = new HttpResponseMessage(statusCode);
		message.Content = new StringContent(newPsbt.ToHex(), Encoding.UTF8, MediaTypeNames.Text.Plain);
		return message;
	}

	private static Task<HttpResponseMessage> PayjoinServerErrorAsync(HttpStatusCode statusCode, string errorCode, string description = "") =>
		Task.FromResult(new HttpResponseMessage(statusCode)
		{
			ReasonPhrase = "",
			Content = new StringContent($$"""{"errorCode": "{{errorCode}}", "message": "{{description}}"}""")
		});

	[Fact]
	public void ApplyOptionalParametersTest()
	{
		var clientParameters = new PayjoinClientParameters();
		clientParameters.Version = 1;
		clientParameters.MaxAdditionalFeeContribution = new Money(50, MoneyUnit.MilliBTC);

		Uri result = PayjoinClient.ApplyOptionalParameters(new Uri("http://test.me/btc/?something=1"), clientParameters);

		// Assert that the final URI does not contain `something=1` and that it contains proper parameters (in lowercase!).
		Assert.Equal("http://test.me/btc/?v=1&disableoutputsubstitution=false&maxadditionalfeecontribution=5000000", result.AbsoluteUri);
	}

	[Fact]
	public void LazyPayjoinServerTest()
	{
		// This tests the scenario where the payjoin server returns the same
		// transaction that we sent to it and adds no inputs. This can give
		// us the fake sense of privacy but it should be valid.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt => psbt);

		var payjoinClient = NewPayjoinClient(mockHttpClient);
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			TestRandom.Get(),
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

		var allowedCoins = transactionFactory.Coins.ToArray();

		var amount = Money.Coins(0.001m);
		using Key key = new();
		PaymentIntent payment = new(key.PubKey, amount);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

		Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
		Assert.True(tx.Signed);
		Assert.Single(tx.InnerWalletOutputs);
		Assert.Single(tx.OuterWalletOutputs);
	}

	[Fact]
	public void HonestPayjoinServerTest()
	{
		var rnd = TestRandom.Get();
		var amountToPay = Money.Coins(0.001m);

		// This tests the scenario where the payjoin server behaves as expected.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var clientTx = psbt.ExtractTransaction();
				foreach (var input in clientTx.Inputs)
				{
					input.WitScript = WitScript.Empty;
				}
				var serverCoinKey = new Key();
				var serverCoin = Coin(0.345m, serverCoinKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
				clientTx.Inputs.Add(serverCoin.Outpoint);
				var paymentOutput = clientTx.Outputs.First(x => x.Value == amountToPay);
				paymentOutput.Value += (Money)serverCoin.Amount;
				var newPsbt = PSBT.FromTransaction(clientTx, Network.Main);

				var serverCoinToSign = newPsbt.Inputs.FindIndexedInput(serverCoin.Outpoint);
				Assert.NotNull(serverCoinToSign);

				serverCoinToSign.UpdateFromCoin(serverCoin);
				serverCoinToSign.Sign(serverCoinKey);
				serverCoinToSign.FinalizeInput();
				return newPsbt;
			});

		var payjoinClient = NewPayjoinClient(mockHttpClient);
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			rnd,
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

		var allowedCoins = transactionFactory.Coins.ToArray();

		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

		Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
		Assert.True(tx.Signed);
		var innerOutput = Assert.Single(tx.InnerWalletOutputs);
		var outerOutput = Assert.Single(tx.OuterWalletOutputs);

		// The payment output is the sum of the original wallet output and the value added by the payee.
		Assert.Equal(0.346m, outerOutput.Amount.ToUnit(MoneyUnit.BTC));
		Assert.Equal(0.09899718m, innerOutput.Amount.ToUnit(MoneyUnit.BTC));

		transactionFactory = ServiceFactory.CreateTransactionFactory(
			rnd,
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			},
			watchOnly: true);
		allowedCoins = transactionFactory.Coins.ToArray();

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

		Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
		Assert.False(tx.Signed);
		innerOutput = Assert.Single(tx.InnerWalletOutputs);
		outerOutput = Assert.Single(tx.OuterWalletOutputs);

		// No payjoin was involved
		Assert.Equal(amountToPay, outerOutput.Amount);
		Assert.Equal(allowedCoins[0].Amount - amountToPay - tx.Fee, innerOutput.Amount);
	}

	[Fact]
	public void DishonestPayjoinServerTest()
	{
		// The server knows one of our utxos and tries to fool the wallet to make it sign the utxo
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var newCoin = psbt.Inputs[0].GetCoin();
				if (newCoin is { })
				{
					newCoin.Outpoint.N = newCoin.Outpoint.N + 1;
					psbt.AddCoins(newCoin);
				}
				return psbt;
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), walletCoins);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		///////

		// The server tries to pay more to itself by taking from the change output
		var destination = BitcoinFactory.CreateScript();
		payment = new PaymentIntent(destination, amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				var diff = Money.Coins(0.0007m);
				var paymentOutput = globalTx.Outputs.Single(x => x.ScriptPubKey == destination);
				var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
				changeOutput.Value -= diff;
				paymentOutput.Value += diff;

				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void BadImplementedPayjoinServerTest()
	{
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);
		var network = Network.Main;

		// This tests the scenario where the payjoin server does not clean GloablXPubs.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.GlobalXPubs.Add(extPubkey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes keypath info in the inputs.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Inputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the inputs sequence.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs[0].Sequence = globalTx.Inputs[0].Sequence + 1;
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server returns an unsigned input (fucking bastard).
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs.Add(GetRandomOutPoint());
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server removes one of our inputs (probably to optimize it).
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs.Clear(); // remove all the inputs
				globalTx.Inputs.Add(GetRandomOutPoint());
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes keypath info in the outputs.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Outputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes partial signatures.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Inputs[0].PartialSigs.Add(new Key().PubKey, new TransactionSignature(new Key().Sign(uint256.One)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the original tx version.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Version += 1;
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the original tx locktime value.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.LockTime = new LockTime(globalTx.LockTime + 1);
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void MinersLoverPayjoinServerTest()
	{
		var rnd = TestRandom.Get();
		// The server wants to make us sign a transaction that pays too much fee
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var destination = BitcoinFactory.CreateScript();
		var payment = new PaymentIntent(destination, amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
				changeOutput.Value -= Money.Coins(0.0007m);
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(rnd, walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void BrokenPayjoinServerTest()
	{
		// The server wants to make us sign a transaction that pays too much fee.
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		var mockHttpClient = new MockIHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerErrorAsync(HttpStatusCode.InternalServerError, "-2345", "Internal Server Error");

		var transactionFactory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Theory]
	[InlineData(ScriptPubKeyType.Segwit)]
	[InlineData(ScriptPubKeyType.TaprootBIP86)]
	public async Task HonestPayjoinSupportsSenderInputType(ScriptPubKeyType scriptType)
	{
		var rnd = TestRandom.Get();
		var keyManager = ServiceFactory.CreateKeyManager("foo", isTaprootAllowed: true);
		var key = keyManager.GenerateNewKey("sender", KeyState.Clean, false, scriptType);
		var coin = BitcoinFactory.CreateSmartCoin(rnd, key, 0.1m);
		await using var store = new AllTransactionStore(".", Network.Main);
		var factory = new TransactionFactory(Network.Main, keyManager, new CoinsView([coin]), store, "foo");
		var destination = BitcoinFactory.CreateScript();
		var amount = Money.Coins(0.001m);
		var mock = new MockIHttpClient();
		mock.OnSendAsync = req => PayjoinServerOkAsync(req, psbt =>
		{
			Assert.All(psbt.Outputs, x => Assert.Empty(x.HDTaprootKeyPaths));
			var tx = psbt.ExtractTransaction();
			foreach (var input in tx.Inputs)
			{
				input.WitScript = WitScript.Empty;
			}
			using var serverKey = new Key();
			var serverCoin = Coin(0.01m, serverKey.PubKey.GetScriptPubKey(scriptType));
			tx.Inputs.Add(serverCoin.Outpoint);
			tx.Outputs.Single(x => x.ScriptPubKey == destination).Value += (Money)serverCoin.Amount;
			var proposal = PSBT.FromTransaction(tx, Network.Main);
			var added = proposal.Inputs.FindIndexedInput(serverCoin.Outpoint)!;
			added.UpdateFromCoin(serverCoin);
			// Taproot signatures commit to all input amounts and scripts.
			foreach (var input in proposal.Inputs)
			{
				var original = psbt.Inputs.FindIndexedInput(input.PrevOut);
				if (original is not null)
				{
					input.WitnessUtxo = original.WitnessUtxo;
				}
			}
			proposal.SignWithKeys(serverKey);
			added.FinalizeInput();
			return proposal;
		});
		var parameters = CreateBuilder().SetPayment(new PaymentIntent(destination, amount)).Build();
		var result = factory.BuildTransaction(parameters, payjoinClient: NewPayjoinClient(mock));

		Assert.Equal(2, result.Transaction.Transaction.Inputs.Count);
		Assert.Equal(amount + Money.Coins(0.01m), result.Transaction.Transaction.Outputs.Single(x => x.ScriptPubKey == destination).Value);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void OutputSubstitutionRespectsPjos(bool disabled)
	{
		var factory = ServiceFactory.CreateTransactionFactory(TestRandom.Get(), [("sender", 0, 0.1m, true, 1)]);
		var destination = BitcoinFactory.CreateScript();
		var replacement = BitcoinFactory.CreateScript();
		var amount = Money.Coins(0.001m);
		var mock = new MockIHttpClient();
		mock.OnSendAsync = req =>
		{
			Assert.Contains($"disableoutputsubstitution={disabled.ToString().ToLowerInvariant()}", req.RequestUri!.Query);
			return PayjoinServerOkAsync(req, psbt =>
			{
				var tx = psbt.GetGlobalTransaction();
				tx.Outputs.Single(x => x.ScriptPubKey == destination).ScriptPubKey = replacement;
				return PSBT.FromTransaction(tx, Network.Main);
			});
		};
		var parameters = CreateBuilder().SetPayment(new PaymentIntent(destination, amount)).Build();
		var client = new PayjoinClient(new Uri("http://localhost"), mock, disabled);
		var result = factory.BuildTransaction(parameters, payjoinClient: client);

		Assert.Contains(result.Transaction.Transaction.Outputs, x => x.ScriptPubKey == (disabled ? destination : replacement) && x.Value == amount);
	}

	private static TransactionParametersBuilder CreateBuilder()
		=> TransactionParametersBuilder.CreateDefault().SetFeeRate(2).SetAllowUnconfirmed(true);

	private static PayjoinClient NewPayjoinClient(IHttpClient client)
		=> new(new Uri("http://localhost"), client);
}
