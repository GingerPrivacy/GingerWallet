using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Rpc;
using WalletWasabi.Daemon.Rpc;
using WalletWasabi.Wallets.SilentPayment;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class SilentPaymentRpcTests
{
	[Theory]
	[InlineData("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", true)]
	[InlineData("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", false)]
	public async Task RpcPaymentConversionRetainsDestinationType(string address, bool silent)
	{
		var service = new PaymentService();
		var handler = new JsonRpcRequestHandler<PaymentService>(service);
		var request = JsonConvert.SerializeObject(new
		{
			jsonrpc = "2.0", id = "1", method = "payment",
			@params = new { payments = new[] { new { sendto = address, amount = "0.1", label = "recipient" } } }
		});
		var response = JObject.Parse(await handler.HandleAsync("", request, CancellationToken.None));
		Assert.True(response["error"] is null, response.ToString());
		Assert.Equal(address, response["result"]!.Value<string>());
		Assert.Equal(silent, service.Payment!.Sendto is SilentPaymentAddress);
		Assert.Equal("recipient", service.Payment.Label);
	}

	[Fact]
	public void RawHexBuildMethodsRejectSilentPaymentsBeforeWalletOrCoinAccess()
	{
		var payment = new PaymentInfo
		{
			Sendto = SilentPaymentAddress.Parse("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", Network.Main),
			Amount = Money.Coins(0.1m), Label = "recipient"
		};
		var service = new WasabiJsonRpcService(null!);
		Assert.Contains("send RPC", Assert.Throws<InvalidOperationException>(() => service.BuildTransaction([payment], [])).Message);
		Assert.Contains("send RPC", Assert.Throws<InvalidOperationException>(() => service.BuildUnsafeTransaction([payment], [])).Message);
	}

	private class PaymentService
	{
		public PaymentInfo? Payment { get; private set; }
		[JsonRpcMethod("payment")]
		public string PaymentDestination(PaymentInfo[] payments)
		{
			Payment = Assert.Single(payments);
			return Payment.Sendto.ToString()!;
		}
	}
}
