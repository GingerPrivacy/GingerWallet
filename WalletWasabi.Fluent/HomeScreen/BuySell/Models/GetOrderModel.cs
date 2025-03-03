using WalletWasabi.BuySell;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Models;

public class GetOrderModel
{
	public GetOrderModel(BuySellClientModels.GetOrderResponseItem model, ProviderModel provider)
	{
		Model = model;
		Provider = provider;
	}

	private BuySellClientModels.GetOrderResponseItem Model { get; }
	private ProviderModel Provider { get; }

	public string RedirectUrl => Model.RedirectUrl.ToString();
	public string OrderId => Model.OrderId;
	public string ProviderName => Provider.Name;
	public string CurrencyFrom => Model.CurrencyFrom;
	public decimal AmountFrom => Model.AmountFrom;
	public DateTimeOffset CreatedAt => Model.CreatedAt.ToLocalTime();

	// Order Type
	public bool IsBuyOrder => Model.OrderType == BuySellClientModels.SupportedFlow.Buy;
	public bool IsSellOrder => Model.OrderType == BuySellClientModels.SupportedFlow.Sell;

	// Statuses
	public bool IsCreated => Model.Status == BuySellClientModels.OrderStatus.Created;
	public bool IsPending => Model.Status == BuySellClientModels.OrderStatus.Pending;
	public bool IsOnHold => Model.Status == BuySellClientModels.OrderStatus.Hold;
	public bool IsRefunded => Model.Status == BuySellClientModels.OrderStatus.Refunded;
	public bool IsExpired => Model.Status == BuySellClientModels.OrderStatus.Expired;
	public bool IsFailed => Model.Status == BuySellClientModels.OrderStatus.Failed;
	public bool IsCompleted => Model.Status == BuySellClientModels.OrderStatus.Complete;

	public int StatusOrderNumber => (int)Model.Status;

	public string GetFormattedAmount()
	{
		if (IsBuyOrder)
		{
			return AmountFrom.ToFormattedFiat(CurrencyFrom);
		}

		return new Amount(AmountFrom).FormattedBtcWithUnit;
	}
}
