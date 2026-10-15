using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Moq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.HomeScreen.Send.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using UiServices = WalletWasabi.Fluent.Services;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

[Collection("Serial unit tests collection")]
public class BatchSendNavigationTests
{
	[AvaloniaTheory]
	[InlineData(2)]
	[InlineData(3)]
	public async Task ReturningFromPreviewPreservesRecipientsAndAllowsEditing(int recipientCount)
	{
		var dataDir = Path.Combine(TestDirectory.Get(), recipientCount.ToString(CultureInfo.InvariantCulture));
		var headers = new SmartHeaderChain();
		await using var index = new IndexStore(Path.Combine(dataDir, "Index"), Network.Main, headers);
		await using var transactions = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, Network.Main);
		await transactions.InitializeAsync();
		var store = new BitcoinStore(index, transactions, new MempoolService(), headers, null!);
		await using var http = new WasabiHttpClientFactory(null, () => new Uri("http://localhost"));
		using var chain = new UnconfirmedTransactionChainProvider(http);
		using var rates = new ExchangeRateService(TimeSpan.FromMinutes(1), http, "USD");
		var directories = new WalletDirectories(Network.Main, dataDir);
		var configuration = new ServiceConfiguration(new IPEndPoint(IPAddress.Loopback, 8333), Money.Satoshis(546));
		var factory = new WalletFactory(dataDir, Network.Main, store, null!, configuration,
			Mock.Of<IWalletFeeRateProvider>(), null!, chain);
		var manager = new WalletManager(Network.Main, dataDir, directories, factory,
			new TwoFactorAuthenticationService(directories, http.SharedWasabiClient));
		var keys = ServiceFactory.CreateWatchOnlyKeyManager();
		keys.SetFilePath(Path.Combine(directories.WalletsDir, "batch.json"));
		using var wallet = manager.AddWallet(keys);
		var previousStore = UiServices.BitcoinStore;
		var previousManager = UiServices.WalletManager;
		SetService(nameof(UiServices.BitcoinStore), store);
		SetService(nameof(UiServices.WalletManager), manager);
		var navigation = new TargettedNavigationStack(NavigationTarget.DialogScreen);

		try
		{
			using var walletModel = new WalletModel(wallet, new AmountProvider(rates));
			var results = new List<TransactionInfo>();
			var editor = new BatchSendDialogViewModel(walletModel, Money.Coins(1m), "", null, LabelsArray.Empty, info =>
			{
				results.Add(info);
				navigation.To(new PreviewPage());
				return Task.CompletedTask;
			});
			navigation.To(editor);
			if (recipientCount == 3)
			{
				editor.AddRecipientCommand.Execute(null);
			}
			var rows = editor.Recipients.ToArray();
			foreach (var (row, i) in rows.Select((row, i) => (row, i)))
			{
				row.To = keys.GenerateNewKey(LabelsArray.Empty, WalletWasabi.Blockchain.Keys.KeyState.Clean, false)
					.P2wpkhScript.GetDestinationAddress(Network.Main)!.ToString();
				row.AmountBtc = (i + 1) * 0.01m;
				row.SuggestionLabels.Labels.Add($"Recipient {i + 1}");
			}
			editor.SubtractFee = true;
			Dispatcher.UIThread.RunJobs();
			await ((ReactiveCommand<Unit, Unit>)editor.NextCommand!).Execute();
			Assert.IsType<PreviewPage>(navigation.CurrentPage);
			Assert.Equal(recipientCount, Assert.Single(results).AllRecipients.Count());

			navigation.Back();
			Assert.Same(editor, navigation.CurrentPage);
			Assert.Equal(rows, editor.Recipients);
			Assert.True(editor.SubtractFee);
			foreach (var (row, recipient) in rows.Zip(results[0].AllRecipients))
			{
				Assert.Equal(recipient.Destination.ToString(), row.To);
				Assert.Equal(recipient.Amount.ToDecimal(MoneyUnit.BTC), row.AmountBtc);
				Assert.Equal(recipient.Label.ToArray(), row.SuggestionLabels.Labels.ToArray());
			}

			rows[^1].To = rows[0].To;
			Assert.False(editor.CanContinue);
			rows[^1].To = results[0].AllRecipients.Last().Destination.ToString()!;
			rows[^1].AmountBtc = 0.04m;
			rows[^1].SuggestionLabels.Labels.Add("Updated");
			Assert.True(editor.CanContinue);
			await ((ReactiveCommand<Unit, Unit>)editor.NextCommand!).Execute();
			Assert.Equal(2, results.Count);
			Assert.Equal(recipientCount, results[1].AllRecipients.Count());
			Assert.Equal(Money.Coins(0.04m), results[1].AdditionalRecipients.Last().Amount);
			Assert.Contains("Updated", results[1].AdditionalRecipients.Last().Label.ToArray());
			Assert.True(results[1].SubtractFee);
		}
		finally
		{
			navigation.Clear();
			SetService(nameof(UiServices.BitcoinStore), previousStore);
			SetService(nameof(UiServices.WalletManager), previousManager);
		}
	}

	private static void SetService(string name, object? value) => typeof(UiServices).GetProperty(name)!.SetValue(null, value);

	private class PreviewPage : RoutableViewModel
	{
	}
}
