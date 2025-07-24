using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.BuySell;
using WalletWasabi.Daemon.BuySell;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Extensions;

public static class BuySellModelExtension
{
	public static CountryModel[] ToModels(this List<BuySellClientModels.CountryInfo> list)
	{
		return list.Select(c => new CountryModel(
				c.Name,
				c.Code,
				c.States?.Select(s => new StateModel(s.Name, s.Code)).OrderBy(x => x.Name).ToArray()))
			.OrderBy(x => x.Name)
			.ToArray();
	}

	public static CurrencyModel[] ToModels(this BuySellClientModels.GetCurrencyListReponse[] list)
	{
		return list.Select(x => new CurrencyModel(x.Ticker, x.Name, int.Parse(x.Precision, CultureInfo.InvariantCulture))).OrderBy(x => x.Ticker).ToArray();
	}
}
