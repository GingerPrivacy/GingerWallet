using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Loading.ViewModels;

public partial class LoadingViewModel : ActivatableViewModel
{
	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.

	public LoadingViewModel(WalletModel wallet)
	{
		Wallet = wallet;
	}

	public WalletModel Wallet { get; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		Wallet.Loader.Progress
			.Do(p => UpdateStatus(p.PercentComplete, p.TimeRemaining))
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void UpdateStatus(double percent, TimeSpan remainingTimeSpan)
	{
		Percent = percent;
		var percentText = Resources.LoadingViewModelPercentCompleted.SafeInject(Percent);

		var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(remainingTimeSpan);
		var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime)
			? ""
			: Resources.LoadingViewModelTimeRemaining.SafeInject(userFriendlyTime);
		StatusText = $"{percentText} {remainingTimeText}";
	}
}
