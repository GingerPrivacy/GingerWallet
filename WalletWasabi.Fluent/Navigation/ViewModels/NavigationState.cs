using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.NavBar.ViewModels;
using WalletWasabi.Fluent.Navigation.Interfaces;

namespace WalletWasabi.Fluent.Navigation.ViewModels;

[AppLifetime]
public class NavigationState : ReactiveObject
{
	private readonly NavBarViewModel _navBar;

	public NavigationState(
		UiContext uiContext,
		INavigationStack<RoutableViewModel> homeScreenNavigation,
		INavigationStack<RoutableViewModel> dialogScreenNavigation,
		INavigationStack<RoutableViewModel> compactDialogScreenNavigation,
		NavBarViewModel navBar)
	{
		_navBar = navBar;
		UiContext = uiContext;
		HomeScreen = homeScreenNavigation;
		DialogScreen = dialogScreenNavigation;
		CompactDialogScreen = compactDialogScreenNavigation;

		this.WhenAnyValue(
				x => x.DialogScreen.CurrentPage,
				x => x.CompactDialogScreen.CurrentPage,
				x => x.HomeScreen.CurrentPage,
				(dialog, compactDialog, mainScreen) => compactDialog ?? dialog ?? mainScreen)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(OnCurrentPageChanged)
			.Subscribe();

		IsDialogOpen =
			this.WhenAnyValue(
				x => x.DialogScreen.CurrentPage,
				x => x.CompactDialogScreen.CurrentPage,
				(d, c) => (d ?? c) != null);
	}

	public UiContext UiContext { get; }

	public INavigationStack<RoutableViewModel> HomeScreen { get; }

	public INavigationStack<RoutableViewModel> DialogScreen { get; }

	public INavigationStack<RoutableViewModel> CompactDialogScreen { get; }

	public IObservable<bool> IsDialogOpen { get; }

	public bool IsAnyPageBusy =>
		HomeScreen.CurrentPage is { IsBusy: true } ||
		DialogScreen.CurrentPage is { IsBusy: true } ||
		CompactDialogScreen.CurrentPage is { IsBusy: true };

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget currentTarget)
	{
		return currentTarget switch
		{
			NavigationTarget.HomeScreen => HomeScreen,
			NavigationTarget.DialogScreen => DialogScreen,
			NavigationTarget.CompactDialogScreen => CompactDialogScreen,
			_ => throw new NotSupportedException(),
		};
	}

	public FluentNavigate To()
	{
		return new FluentNavigate(UiContext);
	}

	public WalletViewModel? To(WalletModel wallet)
	{
		return _navBar.Select(wallet);
	}

	private void OnCurrentPageChanged(RoutableViewModel page)
	{
		if (HomeScreen.CurrentPage is { } homeScreen)
		{
			homeScreen.IsActive = false;
		}

		if (DialogScreen.CurrentPage is { } dialogScreen)
		{
			dialogScreen.IsActive = false;
		}

		if (CompactDialogScreen.CurrentPage is { } compactDialogScreen)
		{
			compactDialogScreen.IsActive = false;
		}

		page.IsActive = true;
	}
}
