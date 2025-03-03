using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Common.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ShuttingDownViewModel : RoutableViewModel
{
	private readonly ApplicationViewModel _applicationViewModel;
	private readonly bool _restart;

	private ShuttingDownViewModel(ApplicationViewModel applicationViewModel, bool restart)
	{
		Title = "Please wait to shut down...";

		_applicationViewModel = applicationViewModel;
		_restart = restart;

		NextCommand = ReactiveCommand.CreateFromTask(
			async () =>
			{
				await UiContext.CoinjoinModel.RestartAbortedCoinjoinsAsync();
				Navigate().Clear();
			});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		RxApp.MainThreadScheduler.Schedule(async () => await UiContext.CoinjoinModel.SignalToStopCoinjoinsAsync());

		Observable.Interval(TimeSpan.FromSeconds(3))
				  .ObserveOn(RxApp.MainThreadScheduler)
				  .Subscribe(_ =>
				  {
					  if (UiContext.CoinjoinModel.CanShutdown())
					  {
						  Navigate().Clear();
						  _applicationViewModel.Shutdown(_restart);
					  }
				  })
				  .DisposeWith(disposables);
	}
}
