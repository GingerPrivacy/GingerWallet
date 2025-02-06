using WalletWasabi.BuySell;

namespace WalletWasabi.Fluent.Models.BuySell;

public class CreateOrderModel
{
	public CreateOrderModel(BuySellClientModels.CreateOrderResponse model)
	{
		Model = model;
	}

	private BuySellClientModels.CreateOrderResponse Model { get; }

	public string RedirectUrl => Model.RedirectUrl.ToString();
}
