using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class CurrencyTests
{
	[Fact]
	private void CurrencyCountTest()
	{
		var currencies = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(culture => new RegionInfo(culture.Name).ISOCurrencySymbol).Where(x => x?.Length == 3).ToImmutableSortedSet();

		Assert.Equal(153, currencies.Count);
	}
}
