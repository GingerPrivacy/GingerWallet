using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

[Collection("Serial unit tests collection")]
public class DualCurrencyEntryBoxTests : IDisposable
{
	private readonly CultureInfo _previousCulture = Resources.Culture;

	public DualCurrencyEntryBoxTests() => Resources.Culture = new GingerCultureInfo("en-US")
	{
		BitcoinFractionGroupSizes = new[] { 4, 4 }, BitcoinTicker = "BTC", FiatTicker = "HUF"
	};

	public void Dispose() => Resources.Culture = _previousCulture;

	[AvaloniaFact]
	public void ClearingWatermarkRemovesApproximateState()
	{
		var entry = new CurrencyEntryBox { Watermark = "≈ 0 HUF" };
		Assert.Contains(":approxwatermark", entry.Classes);

		entry.Watermark = null;

		Assert.Null(entry.Watermark);
		Assert.DoesNotContain(":approxwatermark", entry.Classes);
		entry.Watermark = "0 BTC";
		Assert.DoesNotContain(":approxwatermark", entry.Classes);
		entry.Watermark = "≈ 0 HUF";
		Assert.Contains(":approxwatermark", entry.Classes);
	}

	[AvaloniaTheory]
	[InlineData("1000", 26455000, 3780)]
	[InlineData("1", 3, 33333333)]
	[InlineData("1", 6, 16666666)]
	[InlineData("0.01", 30000, 33)]
	public void FiatInputProducesValidBatchRecipientAmount(string fiatText, int rate, long expectedSatoshis)
	{
		var entry = new DualCurrencyEntryBox { ConversionRate = rate, CurrencyCode = "BTC", ConversionCurrencyCode = "HUF" };
		var window = new Window { Content = entry, Width = 600, Height = 150 };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.NotNull(entry.RightEntryBox);
			Assert.True(entry.RightEntryBox!.Focus());
			entry.RightEntryBox.Text = fiatText;
			Dispatcher.UIThread.RunJobs();

			var expected = Money.Satoshis(expectedSatoshis);
			Assert.Equal(expected.ToDecimal(MoneyUnit.BTC), entry.AmountBtc);
			using Key key = new();
			Assert.True(RecipientInfo.TryCreate(key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString(),
				entry.AmountBtc, LabelsArray.Empty, Network.Main, out var recipient, out var error), error);
			Assert.Equal(expected, recipient!.Amount);

			Assert.True(entry.LeftEntryBox!.Focus());
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(expected.ToDecimal(MoneyUnit.BTC), entry.AmountBtc);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void BitcoinInputPreservesSatoshisAndClearingResetsAmount()
	{
		var entry = new DualCurrencyEntryBox { ConversionRate = 26455000, CurrencyCode = "BTC", ConversionCurrencyCode = "HUF" };
		var window = new Window { Content = entry, Width = 600, Height = 150 };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.True(entry.LeftEntryBox!.Focus());
			entry.LeftEntryBox.Text = "0.00003781";
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(0.00003781m, entry.AmountBtc);

			entry.LeftEntryBox.Text = "";
			Dispatcher.UIThread.RunJobs();
			Assert.Null(entry.AmountBtc);
		}
		finally
		{
			window.Close();
		}
	}
}
