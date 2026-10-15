using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.HomeScreen.Send.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Send.Views;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using UiServices = WalletWasabi.Fluent.Services;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

[Collection("Serial unit tests collection")]
public class BatchSendNavigationTests : IDisposable
{
	private readonly CultureInfo _previousCulture = Resources.Culture;

	public BatchSendNavigationTests() => Resources.Culture = new GingerCultureInfo("en-US")
	{
		BitcoinFractionGroupSizes = new[] { 4, 4 }, BitcoinTicker = "BTC", FiatTicker = "HUF"
	};

	public void Dispose() => Resources.Culture = _previousCulture;

	[AvaloniaTheory]
	[InlineData(2)]
	[InlineData(3)]
	public Task ReturningFromPreviewPreservesRecipientsAndAllowsEditing(int recipientCount) =>
		WithEditorAsync($"preview-{recipientCount}", async (editor, navigation, address) =>
		{
			AddRecipients(editor, recipientCount);
			var rows = editor.Recipients.ToArray();
			foreach (var (row, i) in rows.Select((row, i) => (row, i)))
			{
				FillRecipient(row, address(), i + 1);
			}
			editor.SubtractFee = true;
			Dispatcher.UIThread.RunJobs();
			await ContinueAsync(editor);
			Assert.IsType<PreviewPage>(navigation.CurrentPage);
			Assert.Equal(recipientCount, Assert.Single(editor.Previews).AllRecipients.Count());

			navigation.Back();
			Assert.Same(editor, navigation.CurrentPage);
			Assert.Equal(rows, editor.Recipients);
			Assert.True(editor.SubtractFee);
			foreach (var (row, recipient) in rows.Zip(editor.Previews[0].AllRecipients))
			{
				Assert.Equal(recipient.Destination.ToString(), row.To);
				Assert.Equal(recipient.Amount.ToDecimal(MoneyUnit.BTC), row.AmountBtc);
				Assert.Equal(recipient.Label.ToArray(), row.SuggestionLabels.Labels.ToArray());
			}

			rows[^1].To = rows[0].To;
			Assert.False(editor.BatchCanContinue);
			rows[^1].To = editor.Previews[0].AllRecipients.Last().Destination.ToString()!;
			rows[^1].AmountBtc = 0.04m;
			rows[^1].SuggestionLabels.Labels.Add("Updated");
			Assert.True(editor.BatchCanContinue);
			await ContinueAsync(editor);
			Assert.Equal(2, editor.Previews.Count);
			Assert.Equal(recipientCount, editor.Previews[1].AllRecipients.Count());
			Assert.Equal(Money.Coins(0.04m), editor.Previews[1].AdditionalRecipients.Last().Amount);
			Assert.Contains("Updated", editor.Previews[1].AdditionalRecipients.Last().Label.ToArray());
			Assert.True(editor.Previews[1].SubtractFee);
		});

	[AvaloniaTheory]
	[InlineData(0)]
	[InlineData(1)]
	public Task DeletingEitherOfTwoRecipientsReturnsToSingleSend(int deletedIndex) =>
		WithEditorAsync($"delete-{deletedIndex}", async (editor, navigation, address) =>
		{
			AddRecipients(editor, 2);
			var rows = editor.Recipients.ToArray();
			FillRecipient(rows[0], address(), 1);
			FillRecipient(rows[1], address(), 2);
			editor.SubtractFee = true;
			Dispatcher.UIThread.RunJobs();

			rows[deletedIndex].RemoveCommand.Execute(null);
			Dispatcher.UIThread.RunJobs();
			var survivor = rows[1 - deletedIndex];
			Assert.Same(survivor, Assert.Single(editor.Recipients));
			Assert.False(editor.IsPayToMany);
			Assert.Equal(survivor.To, editor.To);
			Assert.Equal(survivor.AmountBtc, editor.AmountBtc);
			Assert.Same(survivor.SuggestionLabels, editor.SuggestionLabels);
			Assert.Equal(new[] { $"Recipient {2 - deletedIndex}" }, editor.SuggestionLabels.Labels);
			Assert.True(editor.SubtractFee);
			survivor.RemoveCommand.Execute(null);
			Assert.Same(survivor, Assert.Single(editor.Recipients));

			await ContinueAsync(editor);
			var preview = Assert.Single(editor.Previews);
			Assert.False(preview.IsPayToMany);
			Assert.Empty(preview.AdditionalRecipients);
			Assert.False(preview.IsFixedAmount);
			Assert.Equal(survivor.To, preview.Destination.ToString());
			Assert.Equal(Money.Coins(survivor.AmountBtc!.Value), preview.Amount);
			Assert.Equal(survivor.SuggestionLabels.Labels, preview.Recipient.ToArray());
			Assert.False(preview.SubtractFee);
			navigation.Back();
			Assert.Same(survivor, Assert.Single(editor.Recipients));
			Assert.False(editor.IsPayToMany);
			var uriDestination = address();
			editor.To = $"bitcoin:{uriDestination}?amount=0.03&label=Invoice";
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(uriDestination, editor.To);
			Assert.Equal(0.03m, editor.AmountBtc);
			Assert.Equal(new[] { "Invoice" }, editor.SuggestionLabels.Labels);
			Assert.True(editor.IsFixedAmount);
			Assert.False(editor.PayToManyCommand.CanExecute(null));
		});

	[AvaloniaFact]
	public Task DeletingOneOfThreeRecipientsKeepsBatchSendAndRemainingOrder() =>
		WithEditorAsync("delete-middle", async (editor, _, address) =>
		{
			AddRecipients(editor, 3);
			var rows = editor.Recipients.ToArray();
			foreach (var (row, i) in rows.Select((row, i) => (row, i)))
			{
				FillRecipient(row, address(), i + 1);
			}
			rows[1].RemoveCommand.Execute(null);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(new[] { rows[0], rows[2] }, editor.Recipients);
			Assert.True(editor.IsPayToMany);
			await ContinueAsync(editor);
			var preview = Assert.Single(editor.Previews);
			Assert.True(preview.IsPayToMany);
			Assert.True(preview.IsFixedAmount);
			Assert.Equal(new[] { rows[0].To, rows[2].To }, preview.AllRecipients.Select(x => x.Destination.ToString()));
		});

	[AvaloniaFact]
	public Task AddingAfterReturningToSingleKeepsSurvivorAndStartsEmptyRecipient() =>
		WithEditorAsync("add-after-delete", async (editor, navigation, address) =>
		{
			AddRecipients(editor, 2);
			FillRecipient(editor.Recipients[0], address(), 1);
			FillRecipient(editor.Recipients[1], address(), 2);
			var survivor = editor.Recipients[1];
			editor.SubtractFee = true;
			Dispatcher.UIThread.RunJobs();
			editor.Recipients[0].RemoveCommand.Execute(null);
			Dispatcher.UIThread.RunJobs();
			editor.PayToManyCommand.Execute(null);
			Assert.True(editor.IsPayToMany);
			Assert.Equal(2, editor.Recipients.Count);
			Assert.Same(survivor, editor.Recipients[0]);
			var added = editor.Recipients[1];
			Assert.Empty(added.To);
			Assert.Null(added.AmountBtc);
			Assert.Empty(added.SuggestionLabels.Labels);
			Assert.True(editor.SubtractFee);
			FillRecipient(added, address(), 3);
			Dispatcher.UIThread.RunJobs();
			await ContinueAsync(editor);
			var preview = Assert.Single(editor.Previews);
			Assert.Equal(new[] { survivor.To, added.To }, preview.AllRecipients.Select(x => x.Destination.ToString()));
			Assert.Equal(new[] { "Recipient 2", "Recipient 3" }, preview.AllRecipientLabels.ToArray());
			Assert.True(preview.SubtractFee);
			navigation.Back();
			Assert.Equal(new[] { survivor, added }, editor.Recipients);
		});

	[AvaloniaFact]
	public Task SendViewSwitchesFromTwoActiveDeleteButtonsToSingleRecipientFields() =>
		WithEditorAsync("render-switch", (editor, _, address) =>
		{
			AddRecipients(editor, 2);
			FillRecipient(editor.Recipients[0], address(), 1);
			FillRecipient(editor.Recipients[1], address(), 2);
			var survivor = editor.Recipients[1];
			var view = new SendView { DataContext = editor };
			var window = new Window { Content = view, Width = 1200, Height = 1000 };
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();
				var deletes = view.GetVisualDescendants().OfType<Button>()
					.Where(x => Equals(x.Content, Resources.Delete) && x.IsEffectivelyVisible).ToArray();
				Assert.Equal(2, deletes.Length);
				Assert.All(deletes, button => Assert.True(button.IsEffectivelyEnabled));
				Assert.False(view.FindControl<TextBox>("toTb")!.IsEffectivelyVisible);
				Capture(window, "two-recipients");
				deletes[0].Command!.Execute(null);
				Dispatcher.UIThread.RunJobs();
				Assert.Same(survivor, Assert.Single(editor.Recipients));
				Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(),
					x => Equals(x.Content, Resources.Delete) && x.IsEffectivelyVisible);
				var toField = view.FindControl<TextBox>("toTb")!;
				Assert.True(toField.IsEffectivelyVisible);
				Assert.Equal(survivor.To, toField.Text);
				Assert.True(view.FindControl<DualCurrencyEntryBox>("amountTb")!.IsEffectivelyVisible);
				Assert.Equal(survivor.AmountBtc, view.FindControl<DualCurrencyEntryBox>("amountTb")!.AmountBtc);
				Assert.True(view.FindControl<TagsBox>("LabelTagBox")!.IsEffectivelyVisible);
				Assert.Equal(Resources.WalletSend, view.GetVisualDescendants().OfType<ContentArea>().Single().Title);
				Capture(window, "single-after-delete");
			}
			finally
			{
				window.Close();
			}
			return Task.CompletedTask;
		});

	[AvaloniaFact]
	public Task EnteringBatchBeforeQueuedUriParsingDoesNotApplySinglePaymentParameters() =>
		WithEditorAsync("queued-payment-uri", (editor, _, address) =>
		{
			FillRecipient(editor.Recipients[0], address(), 1);
			Dispatcher.UIThread.RunJobs();
			var uri = $"bitcoin:{address()}?amount=0.25&label=Invoice&pj=https://example.com/payjoin";
			editor.To = uri;
			editor.PayToManyCommand.Execute(null);
			Dispatcher.UIThread.RunJobs();
			Assert.True(editor.IsPayToMany);
			Assert.Equal(uri, editor.Recipients[0].To);
			Assert.Equal(0.01m, editor.Recipients[0].AmountBtc);
			Assert.Equal(new[] { "Recipient 1" }, editor.Recipients[0].SuggestionLabels.Labels);
			Assert.False(editor.IsFixedAmount);
			Assert.False(editor.IsPayJoin);
			Assert.Null(editor.PayJoinEndPoint);
			Assert.False(editor.BatchCanContinue);
			return Task.CompletedTask;
		});

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public Task FixedAmountPaymentUriKeepsSendViewStableWhenEnteredAndCleared(bool payjoin) =>
		WithEditorAsync($"fixed-uri-view-{payjoin}", (editor, _, address) =>
		{
			var destination = address();
			var view = new SendView { DataContext = editor };
			var window = new Window { Content = view, Width = 1200, Height = 1000 };
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();
				var toField = view.FindControl<TextBox>("toTb")!;
				var amountField = view.FindControl<DualCurrencyEntryBox>("amountTb")!;
				toField.Text = $"bitcoin:{destination}?amount=0.00001{(payjoin ? "&pj=https://payjoin.invalid/pj" : "")}";
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(destination, editor.To);
				Assert.Equal(0.00001m, editor.AmountBtc);
				Assert.True(editor.IsFixedAmount);
				Assert.True(amountField.IsReadOnly);
				Assert.Equal(payjoin, editor.IsPayJoin);
				Assert.Equal(payjoin ? "https://payjoin.invalid/pj" : null, editor.PayJoinEndPoint);
				Assert.False(editor.PayToManyCommand.CanExecute(null));
				Capture(window, payjoin ? "single-payjoin-uri" : "single-fixed-amount-uri");
				toField.Text = "";
				Dispatcher.UIThread.RunJobs();
				Assert.Empty(editor.To);
				Assert.Equal(0.00001m, editor.AmountBtc);
				Assert.False(editor.IsFixedAmount);
				Assert.False(amountField.IsReadOnly);
				Assert.False(editor.IsPayJoin);
				Assert.Null(editor.PayJoinEndPoint);
				Assert.True(editor.PayToManyCommand.CanExecute(null));
				editor.PayToManyCommand.Execute(null);
				Dispatcher.UIThread.RunJobs();
				Assert.True(editor.IsPayToMany);
				Assert.Equal(2, editor.Recipients.Count);
				Assert.Equal(0.00001m, editor.Recipients[0].AmountBtc);
			}
			finally
			{
				window.Close();
			}
			return Task.CompletedTask;
		});

	[AvaloniaFact]
	public Task SinglePaymentUriStillParsesFixedAmountAndPreventsAddingRecipients() =>
		WithEditorAsync("payment-uri", async (editor, _, address) =>
		{
			var destination = address();
			editor.To = $"bitcoin:{destination}?amount=0.02&label=Invoice";
			Dispatcher.UIThread.RunJobs();
			Assert.False(editor.IsPayToMany);
			Assert.Equal(destination, editor.To);
			Assert.Equal(0.02m, editor.AmountBtc);
			Assert.True(editor.IsFixedAmount);
			Assert.False(editor.PayToManyCommand.CanExecute(null));
			Assert.Contains("Invoice", editor.SuggestionLabels.Labels);
			await ContinueAsync(editor);
			var preview = Assert.Single(editor.Previews);
			Assert.False(preview.IsPayToMany);
			Assert.True(preview.IsFixedAmount);
			Assert.Equal(destination, preview.Destination.ToString());
			Assert.Equal(Money.Coins(0.02m), preview.Amount);
		});

	private static void AddRecipients(SendViewModel editor, int count)
	{
		while (editor.Recipients.Count < count)
		{
			editor.PayToManyCommand.Execute(null);
		}
	}

	private static void FillRecipient(RecipientRowViewModel row, string address, int number)
	{
		row.To = address;
		row.AmountBtc = number * 0.01m;
		row.SuggestionLabels.Labels.Add($"Recipient {number}");
	}

	private static async Task ContinueAsync(TestSendViewModel editor)
	{
		Dispatcher.UIThread.RunJobs();
		Assert.True(editor.NextCommand!.CanExecute(null));
		await ((ReactiveCommand<Unit, Unit>)editor.NextCommand).Execute();
	}

	private static void Capture(Window window, string name)
	{
		using var frame = window.CaptureRenderedFrame();
		Assert.NotNull(frame);
		if (Environment.GetEnvironmentVariable("GINGER_SEND_SCREENSHOTS") is { Length: > 0 } directory)
		{
			Directory.CreateDirectory(directory);
			frame!.Save(Path.Combine(directory, name + ".png"));
		}
	}

	private static async Task WithEditorAsync(string name, Func<TestSendViewModel, TargettedNavigationStack, Func<string>, Task> test)
	{
		var dataDir = Path.Combine(TestDirectory.Get(), name);
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
		Assert.True(wallet.Coins.TryAdd(BitcoinFactory.CreateSmartCoin(TestRandom.Get(), BitcoinFactory.CreateHdPubKey(keys), 1m)));
		using var hostedServices = new HostedServices();
		hostedServices.Register<CoinJoinManager>(() => new CoinJoinManager(manager, null!, http, null!, null!, null!), "Test coinjoin manager");
		var previousStore = UiServices.BitcoinStore;
		var previousManager = UiServices.WalletManager;
		var previousHostedServices = UiServices.HostedServices;
		var previousUiConfig = UiServices.UiConfig;
		var previousContext = UiContext.Default;
		SetService(nameof(UiServices.BitcoinStore), store);
		SetService(nameof(UiServices.WalletManager), manager);
		SetService(nameof(UiServices.HostedServices), hostedServices);
		SetService(nameof(UiServices.UiConfig), new UiConfig());
		var navigation = new TargettedNavigationStack(NavigationTarget.DialogScreen);

		try
		{
			// Avoid application settings construction, which applies startup and update settings.
			// These tests only read the default send preferences from the context.
			UiContext.Default = (UiContext)RuntimeHelpers.GetUninitializedObject(typeof(UiContext));
			typeof(UiContext).GetField("<ApplicationSettings>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(UiContext.Default, RuntimeHelpers.GetUninitializedObject(typeof(ApplicationSettings)));
			typeof(UiContext).GetField("<QrCodeReader>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(UiContext.Default, new QrCodeReader());
			using var walletModel = new WalletModel(wallet, new AmountProvider(rates));
			var editor = new TestSendViewModel(walletModel, new SendFlowModel(wallet, walletModel), navigation);
			navigation.To(editor);
			Dispatcher.UIThread.RunJobs();
			await test(editor, navigation, () => keys.GenerateNewKey(LabelsArray.Empty, WalletWasabi.Blockchain.Keys.KeyState.Clean, false)
				.P2wpkhScript.GetDestinationAddress(Network.Main)!.ToString());
		}
		finally
		{
			navigation.Clear();
			Dispatcher.UIThread.RunJobs();
			UiContext.Default = previousContext;
			SetService(nameof(UiServices.BitcoinStore), previousStore);
			SetService(nameof(UiServices.WalletManager), previousManager);
			SetService(nameof(UiServices.HostedServices), previousHostedServices);
			SetService(nameof(UiServices.UiConfig), previousUiConfig);
		}
	}

	private static void SetService(string name, object? value) => typeof(UiServices).GetProperty(name)!.SetValue(null, value);

	private class TestSendViewModel(WalletModel wallet, SendFlowModel parameters, TargettedNavigationStack navigation)
		: SendViewModel(wallet, parameters)
	{
		public List<TransactionInfo> Previews { get; } = new();

		protected override Task OnPreviewAsync(TransactionInfo transactionInfo)
		{
			Previews.Add(transactionInfo);
			navigation.To(new PreviewPage());
			return Task.CompletedTask;
		}
	}

	private class PreviewPage : RoutableViewModel
	{
	}
}
