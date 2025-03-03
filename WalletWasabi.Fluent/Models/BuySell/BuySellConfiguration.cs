using System.Data.SqlTypes;
using WalletWasabi.BuySell;

namespace WalletWasabi.Fluent.Models.BuySell;

public record BuySellConfiguration(
	CountrySelection? BuyCountry = null,
	CountrySelection? SellCountry = null,
	CurrencyModel? BuyCurrency = null,
	string? TestingAddress = null,
	string? BuyPaymentMethod = null);
