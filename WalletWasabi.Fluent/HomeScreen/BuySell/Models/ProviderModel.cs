using WalletWasabi.BuySell;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Models;

public class ProviderModel
{
	public ProviderModel(BuySellClientModels.GetProvidersListReponse model)
	{
		Model = model;
	}

	private BuySellClientModels.GetProvidersListReponse Model { get; }

	public string Code => Model.Code;

	public string Name => Model.Name;
}
