using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.CoinList.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinDetailsViewModel : RoutableViewModel
{
	private readonly CoinModel _coin;

	[AutoNotify] private string? _bannedTimeText;
	[AutoNotify] private string? _bannedReasonText;

	public CoinDetailsViewModel(CoinModel coin)
	{
		_coin = coin;
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

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_coin.SubscribeToCoinChanges(disposables);

		this.WhenAnyValue(x => x._coin.BannedUntilUtc)
			.Select(x => x is { } time ? Resources.CantParticipateInCoinjoinUntil.SafeInject(time.ToString("g", Resources.Culture)) : null)
			.BindTo(this, x => x.BannedTimeText)
			.DisposeWith(disposables);

		this.WhenAnyValue(x => x._coin.BanReason)
			.Select(x => x is { } reason ? Resources.Reason.SafeInject(reason) : null)
			.BindTo(this, x => x.BannedReasonText)
			.DisposeWith(disposables);
	}
}
