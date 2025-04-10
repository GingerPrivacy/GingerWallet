using System.Threading.Tasks;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Navigation.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Navigation.Interfaces;

public interface INavigate : IWalletNavigation
{
	INavigationStack<RoutableViewModel> HomeScreen { get; }

	INavigationStack<RoutableViewModel> DialogScreen { get; }

	INavigationStack<RoutableViewModel> CompactDialogScreen { get; }

	IObservable<bool> IsDialogOpen { get; }

	bool IsAnyPageBusy { get; }

	INavigationStack<RoutableViewModel> Navigate(NavigationTarget target);

	FluentNavigate To();

	Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target = NavigationTarget.Default, NavigationMode navigationMode = NavigationMode.Normal);
}
