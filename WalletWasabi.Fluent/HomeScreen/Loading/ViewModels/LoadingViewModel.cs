using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Loading.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.HomeScreen)]
public partial class LoadingViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;

	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.
	[AutoNotify] private bool _isLoading;

	public LoadingViewModel(WalletModel wallet)
	{
		_wallet = wallet;
	}

	public string WalletName => _wallet.Name;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Loader.Progress
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
