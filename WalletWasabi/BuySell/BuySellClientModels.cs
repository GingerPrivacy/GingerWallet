using Newtonsoft.Json;
using System.Collections.Generic;

namespace WalletWasabi.BuySell;

public class BuySellClientModels
{
	#region GetOffers

	/// <summary>
	/// Represents the request parameters for fetching on-ramp offers.
	/// </summary>
	public record GetOffersRequest
	{
		/// <summary>
		/// The ticker of the pay-in currency in uppercase (e.g., "USD").
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// The ticker of the payout currency in uppercase (e.g., "BTC").
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// The amount of currency the user is going to pay.
		/// Must be greater than 0.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// The country code in ISO 3166-1 Alpha-2 format (e.g., "US").
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// Optional. The On-Ramp provider code to filter offers by a specific provider.
		/// </summary>
		public string? ProviderCode { get; set; }

		/// <summary>
		/// Optional. The external user ID provided by you for tracking purposes.
		/// </summary>
		public string? ExternalUserID { get; set; }

		/// <summary>
		/// Optional. The state code in ISO 3166-2 format.
		/// Required if the country code is "US".
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// Optional. The user's IP address for geolocation or tracking purposes.
		/// </summary>
		public string? Ip { get; set; }
	}

	/// <summary>
	/// Represents an on-ramp offer returned by the API.
	/// </summary>
	public record GetOffersResponse
	{
		/// <summary>
		/// The On-Ramp provider code (e.g., "provider1"). Represents the entity facilitating the transaction.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// The best rate of purchase among all payment methods. The rate includes all fees.
		/// </summary>
		public required decimal Rate { get; set; }

		/// <summary>
		/// The inverted rate of purchase (e.g., 1/rate).
		/// </summary>
		public required decimal InvertedRate { get; set; }

		/// <summary>
		/// The lowest value of the total fee of purchase among all payment methods.
		/// </summary>
		public required decimal Fee { get; set; }

		/// <summary>
		/// The amount of currency the user is going to pay.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// The largest amount of funds among all payment methods that the user is expected to get after the purchase.
		/// </summary>
		public required decimal AmountExpectedTo { get; set; }

		/// <summary>
		/// A list of detailed offers for each available payment method.
		/// </summary>
		public required List<PaymentMethodOffer> PaymentMethodOffers { get; set; }
	}

	/// <summary>
	/// Represents the details of a payment method offer for an on-ramp transaction.
	/// </summary>
	public record PaymentMethodOffer
	{
		/// <summary>
		/// The amount of funds the user is expected to receive after the purchase.
		/// </summary>
		public required decimal AmountExpectedTo { get; set; }

		/// <summary>
		/// The payment method code (e.g., "card" for credit card).
		/// </summary>
		public required string Method { get; set; }

		/// <summary>
		/// The name of the payment method (e.g., "Credit Card").
		/// </summary>
		public required string MethodName { get; set; }

		/// <summary>
		/// The current rate for the purchase, which includes all applicable fees.
		/// </summary>
		public required decimal Rate { get; set; }

		/// <summary>
		/// The inverted rate for the purchase (e.g., 1/rate).
		/// </summary>
		public required decimal InvertedRate { get; set; }

		/// <summary>
		/// The total fee for the purchase, including all applicable charges.
		/// </summary>
		public required decimal Fee { get; set; }
	}

	#endregion GetOffers

	#region AvailableCountriesRequest

	public enum SupportedFlow
	{
		Buy,
		Sell
	}

	public record GetAvailableCountriesRequest
	{
		/// <summary>
		/// The provider code. Possible values. If the provider code is not specified, the endpoint will return the supported currencies for all providers.
		/// </summary>

		public string? ProviderCode { get; set; }

		/// <summary>
		/// Enum: buy/sell. If the flow is not specified, the endpoint will return result for both cases(On-Ramp buy and Off-Ramp sell).
		/// </summary>
		public SupportedFlow? SupportedFlow { get; set; }
	}

	/// <summary>
	/// Represents the successful response for the list of countries.
	/// </summary>
	public record GetAvailableCountriesResponse
	{
		/// <summary>
		/// The list of countries.
		/// </summary>
		public required List<CountryInfo> Countries { get; set; }
	}

	/// <summary>
	/// Represents information about a country.
	/// </summary>
	public record CountryInfo
	{
		/// <summary>
		/// The ISO 3166-1 code (Alpha-2) of the country.
		/// </summary>
		public required string Code { get; set; }

		/// <summary>
		/// The name of the country.
		/// </summary>
		public required string Name { get; set; }

		/// <summary>
		/// List of states within the country. Returned only if the country code is "US".
		/// </summary>
		public List<StateInfo>? States { get; set; }
	}

	/// <summary>
	/// Represents information about a state in the US.
	/// </summary>
	public record StateInfo
	{
		/// <summary>
		/// The ISO 3166-2 code of the state.
		/// </summary>
		public required string Code { get; set; }

		/// <summary>
		/// The name of the state.
		/// </summary>
		public required string Name { get; set; }
	}

	#endregion AvailableCountriesRequest

	#region GetCurrencyList

	public enum CurrencyType
	{
		Fiat,
		Crypto
	}

	public record GetCurrencyListRequest
	{
		/// <summary>
		/// Enum: crypto/fiat. If the currency type is not specified, the endpoint will return both fiat currencies and cryptocurrencies.
		/// </summary>
		public CurrencyType? CurrencyType { get; set; }

		public string? ProviderCode { get; set; }

		/// <summary>
		/// Enum: buy/sell. If the flow is not specified, the endpoint will return result for both cases(On-Ramp buy and Off-Ramp sell).
		/// </summary>
		public SupportedFlow? SupportedFlow { get; set; }
	}

	public record GetCurrencyListReponse
	{
		/// <summary>
		/// Currency ticker in uppercase. It is a unique identifier of the currency.
		/// </summary>
		public required string Ticker { get; set; }

		/// <summary>
		/// Currency name that you can specify in your interface.
		/// </summary>
		public required string Name { get; set; }

		/// <summary>
		/// Currency type. Possible values are "crypto" or "fiat".
		/// </summary>
		public CurrencyType? Type { get; set; }

		/// <summary>
		/// Extra ID name of the cryptocurrency, for example, "Memo".
		/// For fiat currencies and cryptocurrencies without an extra ID, this value is null.
		/// </summary>
		public required string ExtraIdName { get; set; }

		/// <summary>
		/// URL of the currency icon.
		/// </summary>
		public required string IconUrl { get; set; }

		/// <summary>
		/// Currency precision. For fiat currencies, it is always 2.
		/// </summary>
		public required string Precision { get; set; }
	}

	#endregion GetCurrencyList

	#region GetProviders

	public record GetProvidersListReponse
	{
		/// <summary>
		/// Provider's code.
		/// </summary>
		public required string Code { get; set; }

		/// <summary>
		/// Provider's name.
		/// </summary>
		public required string Name { get; set; }

		/// <summary>
		/// Provider's rating on Trustpilot.
		/// </summary>
		public required string TrustPilotRating { get; set; }

		/// <summary>
		/// URL of the currency icon.
		/// </summary>
		public required string IconUrl { get; set; }
	}

	#endregion GetProviders

	#region ValidateWalletAddress

	public record ValidateWalletAddressRequest
	{
		/// <summary>
		/// Cryptocurrency ticker (in uppercase).
		/// </summary>
		public required string Currency { get; set; }

		/// <summary>
		/// Recipient wallet address.
		/// </summary>
		public required string WalletAddress { get; set; }

		/// <summary>
		/// Property required for wallet addresses of currencies that use an additional ID for transaction processing (XRP, XLM, EOS, BNB).
		/// </summary>
		public string? WalletExtraId { get; set; }
	}

	public enum WalletAddressCause
	{
		WalletAddress,
		WalletExtraId
	}

	public record ValidateWalletAddressResponse
	{
		/// <summary>
		/// False if the wallet address or extra ID is incorrect.
		/// </summary>
		public required bool Result { get; set; }

		/// <summary>
		/// Specifies whether the wallet address or extra ID is incorrect. If Result is true, cause is null.
		/// </summary>
		public required WalletAddressCause? Cause { get; set; }
	}

	#endregion ValidateWalletAddress

	#region CreateOrders

	public record CreateOrderRequest
	{
		/// <summary>
		/// Order ID provided by you.
		/// </summary>
		public required string ExternalOrderId { get; set; }

		/// <summary>
		/// User ID provided by you.
		/// </summary>
		public required string ExternalUserId { get; set; }

		/// <summary>
		/// The On-Ramp provider code. Possible values.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// Ticker of the pay-in currency in uppercase.
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// Ticker of the payout currency in uppercase.
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// Amount of currency the user is going to pay.
		/// </summary>
		public required string AmountFrom { get; set; }

		/// <summary>
		/// Country ISO 3166-1 code (Alpha-2).
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// State ISO 3166-2 code. Is required if provided country is US.
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// User's IP address.
		/// </summary>
		public string? Ip { get; set; }

		/// <summary>
		/// Recipient wallet address.
		/// If you want to provide the cryptocurrency purchase service, you should enable the user to specify the wallet address.
		/// If you want to sell your products for fiat and receive cryptocurrency in your wallet, you should specify your own wallet address.
		/// </summary>
		public required string WalletAddress { get; set; }

		/// <summary>
		/// Property required for wallet addresses of currencies that use an additional ID for transaction processing (XRP, XLM, EOS, BNB).
		/// </summary>
		public string? WalletExtraId { get; set; }

		/// <summary>
		/// The payment method code. Possible values.
		/// </summary>
		public required string PaymentMethod { get; set; }

		/// <summary>
		/// User Agent.
		/// </summary>
		public string? UserAgent { get; set; }

		/// <summary>
		/// Metadata object, which can contain any parameters you need.
		/// </summary>
		public object? Metadata { get; set; }
	}

	public record CreateOrderResponse
	{
		/// <summary>
		/// URL to the provider's purchase page.
		/// </summary>
		public required Uri RedirectUrl { get; set; }

		/// <summary>
		/// Internal order ID provided by Fiat API.
		/// </summary>
		public required string OrderId { get; set; }

		/// <summary>
		/// User ID provided by you.
		/// </summary>
		public required string ExternalUserId { get; set; }

		/// <summary>
		/// Order ID provided by you.
		/// </summary>
		public required string ExternalOrderId { get; set; }

		/// <summary>
		/// The On-Ramp provider code. Possible values.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// Ticker of the pay-in currency in uppercase.
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// Ticker of the payout currency in uppercase.
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// Amount of currency the user is going to pay.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// Country ISO 3166-1 code (Alpha-2).
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// State ISO 3166-2 code. Is required if provided country is US.
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// User's IP address.
		/// </summary>
		public string? Ip { get; set; }

		/// <summary>
		/// Recipient wallet address.
		/// </summary>
		public required string WalletAddress { get; set; }

		/// <summary>
		/// Property required for wallet addresses of currencies that use an additional ID for transaction processing (XRP, XLM, EOS, BNB).
		/// </summary>
		public string? WalletExtraId { get; set; }

		/// <summary>
		/// The payment method code. Possible values.
		/// </summary>
		public required string PaymentMethod { get; set; }

		/// <summary>
		/// User Agent.
		/// </summary>
		public string? UserAgent { get; set; }

		/// <summary>
		/// Metadata object, which can contain any parameters you need:
		/// If you don't provide the metadata object in the request, null will be returned in metadata in response.
		/// If you specify an empty object in the request, an empty object will be returned in the response.
		/// </summary>
		public string? Metadata { get; set; }

		/// <summary>
		/// Time in ISO 8601 format.
		/// </summary>
		public required DateTimeOffset CreatedAt { get; set; }
	}

	#endregion CreateOrders

	#region GetOrder

	public enum OrderStatus
	{
		Created,
		Pending,
		Hold,
		Refunded,
		Expired,
		Failed,
		Complete
	}

	public record GetOrderRequest
	{
		/// <summary>
		/// Start date for filtering orders in ISO format in UTC time zone.
		/// </summary>
		public DateTimeOffset? StartDate { get; set; }

		/// <summary>
		/// End date for filtering orders in ISO format in UTC time zone.
		/// </summary>
		public DateTimeOffset? EndDate { get; set; }

		/// <summary>
		/// Array of order IDs that have been received in response POST /orders or /sell/orders in the orderID field.
		/// </summary>
		public string[]? OrderId { get; set; }

		/// <summary>
		/// Array of external user IDs that you have sent in request POST /orders or /sell/orders in the externalUserID field.
		/// </summary>
		public string[]? ExternalUserId { get; set; }

		/// <summary>
		/// Array of external order IDs that you have sent in request POST /orders or /sell/orders in the externalOrderId field.
		/// </summary>
		public string[]? ExternalOrderId { get; set; }

		/// <summary>
		/// Array of order statuses.
		/// Items Enum: created, pending, hold, refunded, expired, failed, complete.
		/// </summary>
		public OrderStatus[]? Status { get; set; }

		/// <summary>
		/// Skip items to start. Minimum: 0.
		/// </summary>
		public int? Offset { get; set; }

		/// <summary>
		/// Set the maximum number of items. The limit is from 1 to 100.
		/// </summary>
		public int? Limit { get; set; }
	}

	public record GetOrderResponse
	{
		/// <summary>
		/// Array of orders.
		/// </summary>
		public required List<GetOrderResponseItem> Orders { get; set; }

		/// <summary>
		/// Total number of orders.
		/// </summary>
		public required int Total { get; set; }

		/// <summary>
		/// Limit of orders per request.
		/// </summary>
		public required int Limit { get; set; }

		/// <summary>
		/// Offset for paginating orders.
		/// </summary>
		public required int Offset { get; set; }
	}

	[JsonObject(MemberSerialization.OptIn)]
	public record GetOrderResponseItem
	{
		/// <summary>
		/// URL to the provider's purchase page.
		/// </summary>
		[JsonProperty("RedirectUrl", Required = Required.Always)]
		public required Uri RedirectUrl { get; set; }

		/// <summary>
		/// Internal order ID provided by Fiat API.
		/// </summary>
		[JsonProperty("OrderId", Required = Required.Always)]
		public required string OrderId { get; set; }

		/// <summary>
		/// User ID provided by you.
		/// </summary>
		[JsonProperty("ExternalUserId")]
		public string? ExternalUserId { get; set; }

		/// <summary>
		/// Order ID provided by you.
		/// </summary>
		[JsonProperty("ExternalOrderId")]
		public string? ExternalOrderId { get; set; }

		/// <summary>
		/// Order type. Enum: buy/sell.
		/// </summary>
		[JsonProperty("OrderType", Required = Required.Always)]
		public required SupportedFlow OrderType { get; set; }

		/// <summary>
		/// The On-Ramp or Off-Ramp provider code.
		/// </summary>
		[JsonProperty("ProviderCode", Required = Required.Always)]
		public required string ProviderCode { get; set; }

		/// <summary>
		/// Ticker of the pay-in currency in uppercase.
		/// </summary>
		[JsonProperty("CurrencyFrom", Required = Required.Always)]
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// Ticker of the payout currency in uppercase.
		/// </summary>
		[JsonProperty("CurrencyTo", Required = Required.Always)]
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// Amount of currency the user is going to pay.
		/// </summary>
		[JsonProperty("AmountFrom", Required = Required.Always)]
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// Country ISO 3166-1 code (Alpha-2).
		/// </summary>
		[JsonProperty("Country", Required = Required.Always)]
		public required string Country { get; set; }

		/// <summary>
		/// State ISO 3166-2 code. Required if country is US.
		/// </summary>
		[JsonProperty("State")]
		public string? State { get; set; }

		/// <summary>
		/// User's IP address.
		/// </summary>
		[JsonProperty("Ip")]
		public string? Ip { get; set; }

		/// <summary>
		/// Recipient wallet address.
		/// </summary>
		[JsonProperty("WalletAddress")]
		public string? WalletAddress { get; set; }

		/// <summary>
		/// Property required for wallet addresses with extra IDs (XRP, XLM, etc.).
		/// </summary>
		[JsonProperty("WalletExtraId")]
		public string? WalletExtraId { get; set; }

		/// <summary>
		/// Recipient refund address.
		/// </summary>
		[JsonProperty("RefundAddress")]
		public string? RefundAddress { get; set; }

		/// <summary>
		/// The payment method code.
		/// </summary>
		[JsonProperty("PaymentMethod", Required = Required.Always)]
		public required string PaymentMethod { get; set; }

		/// <summary>
		/// User Agent.
		/// </summary>
		[JsonProperty("UserAgent")]
		public string? UserAgent { get; set; }

		/// <summary>
		/// Metadata object.
		/// </summary>
		[JsonProperty("Metadata")]
		public string? Metadata { get; set; }

		/// <summary>
		/// Time in ISO 8601 format.
		/// </summary>
		[JsonProperty("CreatedAt", Required = Required.Always)]
		public required DateTimeOffset CreatedAt { get; set; }

		/// <summary>
		/// Current status of the order. Enum: created/pending/hold/refunded/expired/failed/complete.
		/// </summary>
		[JsonProperty("Status", Required = Required.Always)]
		public required OrderStatus Status { get; set; }

		/// <summary>
		/// Payin amount.
		/// </summary>
		[JsonProperty("PayinAmount")]
		public decimal? PayinAmount { get; set; }

		/// <summary>
		/// Estimated payout amount.
		/// </summary>
		[JsonProperty("PayoutAmount")]
		public decimal? PayoutAmount { get; set; }

		/// <summary>
		/// Ticker of the payin currency.
		/// </summary>
		[JsonProperty("PayinCurrency")]
		public string? PayinCurrency { get; set; }

		/// <summary>
		/// Ticker of the payout currency.
		/// </summary>
		[JsonProperty("PayoutCurrency")]
		public string? PayoutCurrency { get; set; }

		/// <summary>
		/// Time in ISO 8601 format.
		/// </summary>
		[JsonProperty("UpdatedAt", Required = Required.Always)]
		public required DateTimeOffset UpdatedAt { get; set; }
	}

	#endregion GetOrder

	#region GetLimits

	public record GetLimitsRequest
	{
		/// <summary>
		/// The ticker of the pay-in currency in uppercase (e.g., "USD").
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// The ticker of the payout currency in uppercase (e.g., "BTC").
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// The country code in ISO 3166-1 Alpha-2 format (e.g., "US").
		/// </summary>
		public required string Country { get; set; }

		public string? State { get; set; }
	}

	public record GetLimitsResponse
	{
		public required string ProviderCode { get; set; }
		public required string CurrencyFrom { get; set; }
		public required string CurrencyTo { get; set; }
		public decimal? Min { get; set; }
		public decimal? Max { get; set; }
	}

	#endregion GetLimits

	#region GetOffers

	/// <summary>
	/// Represents the request parameters for fetching on-ramp offers.
	/// </summary>
	public record GetSellOffersRequest
	{
		/// <summary>
		/// The ticker of the pay-in currency in uppercase (e.g., "USD").
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// The ticker of the payout currency in uppercase (e.g., "BTC").
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// The amount of currency the user is going to pay.
		/// Must be greater than 0.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// The country code in ISO 3166-1 Alpha-2 format (e.g., "US").
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// Optional. The On-Ramp provider code to filter offers by a specific provider.
		/// </summary>
		public string? ProviderCode { get; set; }

		/// <summary>
		/// Optional. The external user ID provided by you for tracking purposes.
		/// </summary>
		public string? ExternalUserID { get; set; }

		/// <summary>
		/// Optional. The state code in ISO 3166-2 format.
		/// Required if the country code is "US".
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// Optional. The user's IP address for geolocation or tracking purposes.
		/// </summary>
		public string? Ip { get; set; }

		/// <summary>
		/// The payment method code.
		/// </summary>
		public string? PaymentMethod { get; set; }
	}

	/// <summary>
	/// Represents an on-ramp offer returned by the API.
	/// </summary>
	public record GetSellOffersResponse
	{
		/// <summary>
		/// The On-Ramp provider code (e.g., "provider1"). Represents the entity facilitating the transaction.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// The best rate of purchase among all payment methods. The rate includes all fees.
		/// </summary>
		public required decimal Rate { get; set; }

		/// <summary>
		/// The inverted rate of purchase (e.g., 1/rate).
		/// </summary>
		public required decimal InvertedRate { get; set; }

		/// <summary>
		/// The lowest value of the total fee of purchase among all payment methods.
		/// </summary>
		public required decimal Fee { get; set; }

		/// <summary>
		/// The amount of currency the user is going to pay.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// The largest amount of funds among all payment methods that the user is expected to get after the purchase.
		/// </summary>
		public required decimal AmountExpectedTo { get; set; }

		/// <summary>
		/// A list of detailed offers for each available payment method.
		/// </summary>
		public required List<PaymentMethodOffer> PaymentMethodOffers { get; set; }
	}

	#endregion GetOffers

	#region CreateSellOrders

	public record CreateSellOrderRequest
	{
		/// <summary>
		/// Order ID provided by you.
		/// </summary>
		public required string ExternalOrderId { get; set; }

		/// <summary>
		/// User ID provided by you.
		/// </summary>
		public required string ExternalUserId { get; set; }

		/// <summary>
		/// The On-Ramp provider code. Possible values.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// Ticker of the pay-in currency in uppercase.
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// Ticker of the payout currency in uppercase.
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// Amount of currency the user is going to pay.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// Country ISO 3166-1 code (Alpha-2).
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// State ISO 3166-2 code. Is required if provided country is US.
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// User's IP address.
		/// </summary>
		public string? Ip { get; set; }

		/// <summary>
		/// Recipient refund address.
		/// </summary>
		public required string RefundAddress { get; set; }

		/// <summary>
		/// The payment method code. Possible values.
		/// </summary>
		public string? PaymentMethod { get; set; }

		/// <summary>
		/// User Agent.
		/// </summary>
		public string? UserAgent { get; set; }

		/// <summary>
		/// Metadata object, which can contain any parameters you need.
		/// </summary>
		public object? Metadata { get; set; }
	}

	public record CreateSellOrderResponse
	{
		/// <summary>
		/// URL to the provider's purchase page.
		/// </summary>
		public required Uri RedirectUrl { get; set; }

		/// <summary>
		/// Internal order ID provided by Fiat API.
		/// </summary>
		public required string OrderId { get; set; }

		/// <summary>
		/// User ID provided by you.
		/// </summary>
		public required string ExternalUserId { get; set; }

		/// <summary>
		/// Order ID provided by you.
		/// </summary>
		public required string ExternalOrderId { get; set; }

		/// <summary>
		/// The On-Ramp provider code. Possible values.
		/// </summary>
		public required string ProviderCode { get; set; }

		/// <summary>
		/// Ticker of the pay-in currency in uppercase.
		/// </summary>
		public required string CurrencyFrom { get; set; }

		/// <summary>
		/// Ticker of the payout currency in uppercase.
		/// </summary>
		public required string CurrencyTo { get; set; }

		/// <summary>
		/// Amount of currency the user is going to pay.
		/// </summary>
		public required decimal AmountFrom { get; set; }

		/// <summary>
		/// Country ISO 3166-1 code (Alpha-2).
		/// </summary>
		public required string Country { get; set; }

		/// <summary>
		/// State ISO 3166-2 code. Is required if provided country is US.
		/// </summary>
		public string? State { get; set; }

		/// <summary>
		/// User's IP address.
		/// </summary>
		public string? Ip { get; set; }

		/// <summary>
		/// Recipient refund address.
		/// </summary>
		public required string RefundAddress { get; set; }

		/// <summary>
		/// The payment method code. Possible values.
		/// </summary>
		public required string PaymentMethod { get; set; }

		/// <summary>
		/// User Agent.
		/// </summary>
		public string? UserAgent { get; set; }

		/// <summary>
		/// Metadata object, which can contain any parameters you need:
		/// If you don't provide the metadata object in the request, null will be returned in metadata in response.
		/// If you specify an empty object in the request, an empty object will be returned in the response.
		/// </summary>
		public string? Metadata { get; set; }

		/// <summary>
		/// Time in ISO 8601 format.
		/// </summary>
		public required DateTimeOffset CreatedAt { get; set; }
	}

	#endregion CreateSellOrders
}
