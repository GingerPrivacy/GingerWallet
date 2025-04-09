using System.Globalization;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.Lang.Models;

public class GingerCultureInfo : CultureInfo
{
	public static readonly string DefaultLanguage = "en-US";
	public static readonly string DefaultFiatCurrencyTicker = "USD";
	public static readonly string DefaultBitcoinCurrencyTicker = "BTC";
	public static readonly string DefaultGroupSeparator = GroupSeparator.Space.GetChar();
	public static readonly string DefaultDecimalSeparator = DecimalSeparator.Dot.GetChar();
	public static readonly int[] DefaultBitcoinFractionSizes = BtcFractionGroupSize.FourFour.GetGroupSizes();

	public GingerCultureInfo(int culture) : base(culture)
	{
	}

	public GingerCultureInfo(int culture, bool useUserOverride) : base(culture, useUserOverride)
	{
	}

	public GingerCultureInfo(string name) : base(name)
	{
	}

	public GingerCultureInfo(string name, bool useUserOverride) : base(name, useUserOverride)
	{
	}

	public required int[] BitcoinFractionGroupSizes { get; init; }
	public required string BitcoinTicker { get; init; }
	public required string FiatTicker { get; init; }
}
