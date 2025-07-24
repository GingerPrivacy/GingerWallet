using System.Reactive.Linq;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;

public partial class BtcPriceTileViewModel : ActivatableViewModel
{
	[AutoNotify] private bool _isRateAvailable;
	[AutoNotify] private bool _isFetching;

	public BtcPriceTileViewModel(AmountProvider amountProvider)
	{
		ExchangeRateText = amountProvider.ExchangeRateObservable
			.Select(x =>
			{
				IsFetching = x == -1;
				IsRateAvailable = x > 0;
				return IsFetching ? "PLACEHOLDER" : IsRateAvailable ? x.ToFiatFormatted() : Resources.NotAvailable;
			});
	}

	public IObservable<string> ExchangeRateText { get; }
}
