using NBitcoin;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;
using static WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage.CoinJoinTransactionArchiver;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;

/// <summary>
/// Tests for <see cref="CoinJoinTransactionArchiver"/>
/// </summary>
public class CoinJoinTransactionArchiverTests
{
	[Fact]
	public async Task StoreTransactionAsync()
	{
		string tempFolder = Path.GetTempPath();
		CoinJoinTransactionArchiver archiver = new(tempFolder);

		var coins = new[]
{
			("", 0, 1.00118098m, true, 1)
		};

		TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory(coins);
		using Key key = new();
		var payment = new PaymentIntent(new[]
		{
			new DestinationRequest(key, Money.Coins(0.3m))
		});
		var txParameters = TransactionParametersBuilder.CreateDefault().SetFeeRate(2m).SetAllowUnconfirmed(true).SetPayment(payment).SetFeeRate(20m).Build();
		var randomTx = transactionFactory.BuildTransaction(txParameters).Transaction.Transaction;

		DateTimeOffset now = DateTimeOffset.Parse("2021-09-28T20:45:30.3124Z", CultureInfo.InvariantCulture);
		string storagePath = await archiver.StoreJsonAsync(randomTx, now);

		TransactionInfo transactionInfo = JsonSerializer.Deserialize<TransactionInfo>(File.ReadAllText(storagePath))!;
		Assert.NotNull(transactionInfo);
		Assert.Equal(1632861930312, transactionInfo.Created);
		Assert.Equal(randomTx.GetHash().ToString(), transactionInfo.TxHash);
		Assert.Equal(randomTx.ToHex(), transactionInfo.RawTransaction);
	}
}
