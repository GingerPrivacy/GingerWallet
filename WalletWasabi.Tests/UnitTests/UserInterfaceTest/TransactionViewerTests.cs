using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.TransactionViewer.Controls;
using WalletWasabi.Fluent.TransactionViewer.ViewModels;
using WalletWasabi.Fluent.TransactionViewer.Views;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(WalletWasabi.Tests.UnitTests.UserInterfaceTest.TransactionViewerTestAppBuilder))]

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

[Collection("Serial unit tests collection")]
public class TransactionViewerTests : IDisposable
{
	private readonly CultureInfo _previousCulture = Resources.Culture;

	public TransactionViewerTests() => Resources.Culture = new GingerCultureInfo("hu-HU")
	{
		BitcoinFractionGroupSizes = new[] { 4, 4 }, BitcoinTicker = "BTC", FiatTicker = "HUF"
	};

	public void Dispose() => Resources.Culture = _previousCulture;

	[Fact]
	public void UnsignedPreviewKeepsTheBuilderFeeWhenPsbtPrevoutsAreUnavailable()
	{
		var model = CreateModel();
		var result = new BuildTransactionResult(model.Transaction, PSBT.FromTransaction(model.Transaction.Transaction, Network.Main),
			false, Money.Satoshis(622), 0, new());
		Assert.False(result.Psbt.TryGetFee(out _));
		var preview = new TransactionCoinsModel(result, Network.Main, _ => false, new Dictionary<Script, LabelsArray>());
		Assert.True(preview.IsPreview);
		Assert.Equal(result.Fee, preview.Fee);
		Assert.True(preview.VirtualSize > 0);
	}

	[AvaloniaFact]
	public async Task FiatTracksSelectedCurrencyAndRateWithoutInventingUnknownAmounts()
	{
		using var rate = new BehaviorSubject<decimal>(30_000_000m);
		using var vm = new TransactionViewerViewModel(CreateModel(), rate, Observable.Return(false));
		Assert.Equal("≈ 599\u00a0813,40 HUF", await vm.Outputs.Last().Fiat.FirstAsync());
		rate.OnNext(0);
		Assert.Equal("", await vm.Outputs.Last().Fiat.FirstAsync());
		rate.OnNext(20_000_000m);
		Assert.Equal("≈ 399\u00a0875,60 HUF", await vm.Outputs.Last().Fiat.FirstAsync());
		Resources.Culture = new GingerCultureInfo("hu-HU")
		{
			BitcoinFractionGroupSizes = new[] { 4, 4 }, BitcoinTicker = "BTC", FiatTicker = "EUR"
		};
		rate.OnNext(50_000m);
		Assert.Equal("≈ 999,69 EUR", await vm.Outputs.Last().Fiat.FirstAsync());

		using var unknown = new TransactionViewerViewModel(CreateModel(100), rate, Observable.Return(false));
		Assert.Equal(Resources.Unknown, unknown.InputTotal);
		Assert.Equal(Resources.Unknown, unknown.FeeText);
		Assert.Equal("", await unknown.InputFiat.FirstAsync());
		Assert.Equal("", await unknown.Inputs.First().Fiat.FirstAsync());
		Assert.Equal("", await unknown.FeeFiat.FirstAsync());
	}

	[AvaloniaFact]
	public async Task SelectingCardsUpdatesInspectorAndOnlyOneHighlight()
	{
		using var vm = new TransactionViewerViewModel(CreateModel(), Observable.Return(30_000_000m), Observable.Return(false));
		Assert.Same(vm.Outputs.Last(), vm.SelectedNode);
		await vm.Inputs[1].SelectCommand.Execute();
		Assert.Same(vm.Inputs[1], vm.SelectedNode);
		Assert.Equal(Resources.TransactionViewerSelectedInput, vm.SelectionTitle);
		Assert.Single(vm.Inputs.Concat(vm.Outputs), x => x.IsSelected);
		Assert.Equal("P2WPKH", vm.SelectedNode!.ScriptType);
	}

	[AvaloniaFact]
	public async Task CardClickSelectsAndInspectorCopiesTheFullOutpoint()
	{
		var vm = new TransactionViewerViewModel(CreateModel(), Observable.Return(30_000_000m), Observable.Return(false));
		var window = new TransactionViewerWindow { DataContext = vm };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.Contains(window.FindControl<StackPanel>("SelectionDetails")!.GetVisualDescendants().OfType<TextBlock>(),
				x => x.Text == "Dávid, Gabika" && x.IsEffectivelyVisible);
			var card = window.GetVisualDescendants().OfType<Button>().Single(x => ReferenceEquals(x.DataContext, vm.Inputs[1]));
			Click(window, card);
			Assert.Same(vm.Inputs[1], vm.SelectedNode);
			var copy = window.GetVisualDescendants().OfType<Button>().Single(x => x.Tag is OutPoint);
			Click(window, copy);
			Assert.Equal(vm.Inputs[1].Data.OutPoint.ToString(), await window.Clipboard!.GetTextAsync());
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void LargeCoinJoinVirtualizesAndScrollsToTheLastOutput()
	{
		using var privacy = new BehaviorSubject<bool>(false);
		using var rate = new BehaviorSubject<decimal>(30_000_000m);
		var vm = new TransactionViewerViewModel(CreateModel(1200), rate, privacy);
		var window = new TransactionViewerWindow { DataContext = vm };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var list = window.GetVisualDescendants().OfType<ListBox>().Single();
			var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
			Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
			Assert.True(window.GetVisualDescendants().OfType<Grid>().Single(x => x.Name == "FlowOverlay").Bounds.Width > list.Bounds.Width - 20);
			Assert.Equal(vm.Rows.Count * TransactionFlowLines.RowHeight, scroll.Extent.Height);
			var scrollbar = scroll.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Orientation == Avalonia.Layout.Orientation.Vertical);
			Assert.False(scrollbar.AllowAutoHide);
			Assert.True(scrollbar.IsVisible && scrollbar.Opacity == 1 && scrollbar.Bounds.Width >= 6 && scrollbar.Bounds.Height > 200,
				$"Scrollbar: {scrollbar.Bounds}, visible: {scrollbar.IsVisible}, opacity: {scrollbar.Opacity}");
			Assert.InRange(list.GetRealizedContainers().Count(), 1, 20);
			Capture(window, "coinjoin-top");
			scroll.ScrollToEnd();
			Dispatcher.UIThread.RunJobs();
			Assert.True(scroll.Offset.Y > 1000);
			Assert.NotNull(list.ContainerFromIndex(vm.Rows.Count - 1));
			Assert.InRange(list.GetRealizedContainers().Count(), 1, 20);
			Assert.Equal(scroll.Offset.Y, window.GetVisualDescendants().OfType<TransactionFlowLines>().Single().ScrollOffset);
			Capture(window, "coinjoin-bottom");
			var graph = window.FindControl<Grid>("GraphPanel")!;
			var summary = window.FindControl<ScrollViewer>("SummaryPanel")!;
			var graphWidth = graph.Bounds.Width;
			var summaryHeight = summary.Bounds.Height;
			Drag(window, window.FindControl<GridSplitter>("DetailsSplitter")!, new Vector(-140, 0));
			Drag(window, window.FindControl<GridSplitter>("SummarySplitter")!, new Vector(0, -80));
			Assert.True(graph.Bounds.Width < graphWidth - 100);
			Assert.True(summary.Bounds.Height > summaryHeight + 50);
			Capture(window, "panels-resized");
			window.Width = 1040;
			window.Height = 640;
			Dispatcher.UIThread.RunJobs();
			scroll.ScrollToEnd();
			Dispatcher.UIThread.RunJobs();
			Assert.True(graph.Bounds.Width >= 580);
			Assert.True(window.FindControl<Border>("DetailsPanel")!.Bounds.Width >= 260);
			Assert.True(summary.Bounds.Bottom <= window.FindControl<Grid>("TransactionContent")!.Bounds.Height);
			Assert.NotNull(list.ContainerFromIndex(vm.Rows.Count - 1));
			Assert.InRange(list.GetRealizedContainers().Count(), 1, 20);
			var lines = window.GetVisualDescendants().OfType<TransactionFlowLines>().Single();
			Assert.Equal(scroll.Offset.Y, lines.ScrollOffset);
			Assert.InRange(Math.Abs(lines.Bounds.Width - list.Bounds.Width), 0, 20);
			Capture(window, "panels-resized-small");
			privacy.OnNext(true);
			Dispatcher.UIThread.RunJobs();
			Assert.False(window.FindControl<Grid>("TransactionContent")!.IsVisible);
			Capture(window, "privacy");
		}
		finally
		{
			window.Close();
		}
		Assert.False(privacy.HasObservers);
		Assert.False(rate.HasObservers);
	}

	[AvaloniaTheory]
	[InlineData(1280, 850, false)]
	[InlineData(1440, 960, false)]
	[InlineData(1040, 640, false)]
	[InlineData(1280, 850, true)]
	public void ViewerRendersAtMinimumSizeAndInBothThemes(int width, int height, bool light)
	{
		var vm = new TransactionViewerViewModel(CreateModel(), Observable.Return(30_000_000m), Observable.Return(false));
		var window = new TransactionViewerWindow
		{
			DataContext = vm, Width = width, Height = height,
			RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark
		};
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(3, window.GetVisualDescendants().OfType<ListBox>().Single().Items.Count);
			Assert.True(window.GetVisualDescendants().OfType<TransactionFlowLines>().Single().Bounds.Width > width - 420);
			Assert.True(window.FindControl<StackPanel>("SelectionDetails")!.Bounds.Width > 200);
			Capture(window, $"viewer-{width}-{(light ? "light" : "dark")}");
			window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
			Assert.False(window.IsVisible);
		}
		finally
		{
			window.Close();
		}
	}

	private static TransactionCoinsModel CreateModel(int foreignInputs = 0)
	{
		var keys = ServiceFactory.CreateKeyManager();
		var input = BitcoinFactory.CreateHdPubKey(keys);
		var output = BitcoinFactory.CreateHdPubKey(keys, isInternal: true);
		output.SetLabel(new LabelsArray("Dávid", "Gabika"));
		var external = foreignInputs > 0 ? Enumerable.Repeat(Money.Coins(0.001m), foreignInputs) : new[] { Money.Coins(0.07m), Money.Coins(0.03m) };
		var tx = BitcoinFactory.CreateSmartTransaction(TestRandom.Get(), foreignInputs, external,
			new[] { (Money.Coins(0.05m), 20, input), (Money.Coins(0.04m), 20, input), (Money.Coins(0.03m), 20, input) },
			new[] { (Money.Coins(0.01999378m), 20, output) });
		return new TransactionCoinsModel(tx, Network.Main, script => keys.TryGetKeyForScriptPubKey(script, out _));
	}

	private static void Capture(Window window, string name)
	{
		using var frame = window.CaptureRenderedFrame();
		Assert.NotNull(frame);
		if (Environment.GetEnvironmentVariable("GINGER_VIEWER_SCREENSHOTS") is { Length: > 0 } directory)
		{
			Directory.CreateDirectory(directory);
			frame!.Save(Path.Combine(directory, name + ".png"));
		}
	}

	private static void Click(Window window, Button button)
	{
		var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
		window.MouseDown(point, MouseButton.Left);
		window.MouseUp(point, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();
	}

	private static void Drag(Window window, GridSplitter splitter, Vector delta)
	{
		var start = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;
		window.MouseDown(start, MouseButton.Left);
		window.MouseMove(start + delta, RawInputModifiers.LeftMouseButton);
		window.MouseUp(start + delta, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();
	}
}

public static class TransactionViewerTestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TransactionViewerTestApp>()
		.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseReactiveUI();
}

public class TransactionViewerTestApp : Application
{
	public override void Initialize()
	{
		RequestedThemeVariant = ThemeVariant.Dark;
		Styles.Add(new FluentTheme());
		foreach (var path in new[] { "Styles/Themes/Fluent.axaml", "Icons/Icons.axaml", "Styles/Styles.axaml" })
		{
			Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/")) { Source = new Uri($"avares://WalletWasabi.Fluent/{path}") });
		}
	}
}
