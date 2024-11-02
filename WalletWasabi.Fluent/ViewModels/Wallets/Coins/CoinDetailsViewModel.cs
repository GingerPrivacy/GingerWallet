using NBitcoin;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class CoinDetailsViewModel : RoutableViewModel
{
	public CoinDetailsViewModel(UiContext uiContext, ICoinModel coin)
	{
		UiContext = uiContext;
		Title = Resources.CoinDetails;
		EnableBack = true;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		Amount = uiContext.AmountProvider.Create(coin.Amount);
		Address = coin.Address.ToString();
		Index = coin.Index;
		TransactionId = coin.TransactionId;
	}

	public Amount Amount { get; }
	public string Address { get; }
	public int Index { get; }
	public uint256 TransactionId { get; }
}
