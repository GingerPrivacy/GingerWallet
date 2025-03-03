using WalletWasabi.BuySell;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Models;

public class OfferModel
{
	public OfferModel(
		ProviderModel provider,
		decimal amountFrom,
		string currencyFrom,
		string currencyTo,
		string countryCode,
		string? stateCode,
		BuySellClientModels.PaymentMethodOffer model)
	{
		Provider = provider;
		AmountFrom = amountFrom;
		CurrencyFrom = currencyFrom;
		CurrencyTo = currencyTo;
		CountryCode = countryCode;
		StateCode = stateCode;
		Model = model;
	}

	private BuySellClientModels.PaymentMethodOffer Model { get; }
	private ProviderModel Provider { get; }

	public string ProviderCode => Provider.Code;
	public string ProviderName => Provider.Name;
	public decimal AmountFrom { get; }
	public decimal Fee => Model.Fee;
	public string CurrencyFrom { get; }
	public string CurrencyTo { get; }
	public string CountryCode { get; }
	public string? StateCode { get; }
	public decimal AmountTo => Model.AmountExpectedTo;
	public string MethodName => Model.MethodName;
	public string PaymentMethod => Model.Method;
}
