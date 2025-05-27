using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.CoinList.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinDetailsViewModel : RoutableViewModel
{
	public CoinDetailsViewModel(CoinModel coin)
	{
		Title = Resources.CoinDetails;
		EnableBack = true;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		Amount = UiContext.AmountProvider.Create(coin.Amount);
		Address = coin.Address.ToString();
		Index = coin.Index;
		TransactionId = coin.TransactionId;
	}

	public Amount Amount { get; }
	public string Address { get; }
	public int Index { get; }
	public uint256 TransactionId { get; }
}
