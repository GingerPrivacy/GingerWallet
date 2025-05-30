using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Common.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SuccessViewModel : RoutableViewModel
{
	public SuccessViewModel()
	{
		Title = Resources.Success;

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private async Task OnNextAsync()
	{
		await Task.Delay(UiConstants.CloseSuccessDialogMillisecondsDelay);

		UiContext.Navigate(CurrentTarget).Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (NextCommand is not null && NextCommand.CanExecute(default))
		{
			NextCommand.Execute(default);
		}
	}
}
