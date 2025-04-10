using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Announcement.Models;
using WalletWasabi.Fluent.Announcement.ViewModels;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.HomeScreen.Notifications.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.NavBar.ViewModels;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.SearchBar.ViewModels;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;
using WalletWasabi.Fluent.Settings.ViewModels;
using WalletWasabi.Fluent.Status.ViewModels;

namespace WalletWasabi.Fluent.Common.ViewModels;

[AppLifetime]
public partial class MainViewModel : ViewModelBase
{
	[Localizable(false)] [AutoNotify] private string _title = "Ginger Wallet";
	[AutoNotify] private WindowState _windowState;
	[AutoNotify] private bool _isOobeBackgroundVisible;
	[AutoNotify] private bool _isCoinJoinActive;

	public MainViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		UiContext.SetMainViewModel(this);

		ApplyUiConfigWindowState();

		DialogScreen = new DialogScreenViewModel();
		CompactDialogScreen = new DialogScreenViewModel(NavigationTarget.CompactDialogScreen);
		NavBar = new NavBarViewModel(UiContext);
		MainScreen = new TargettedNavigationStack(NavigationTarget.HomeScreen);
		UiContext.RegisterNavigation(new NavigationState(UiContext, MainScreen, DialogScreen, CompactDialogScreen, NavBar));

		StatusIcon = new StatusIconViewModel(UiContext);
		AnnouncementIcon = new AnnouncementIconViewModel(UiContext, new AnnouncementsModel());

		SettingsPage = new SettingsPageViewModel(UiContext);
		PrivacyMode = new PrivacyModeViewModel(UiContext.ApplicationSettings);
		Notifications = new WalletNotificationsViewModel(NavBar);

		NavigationManager.RegisterType(NavBar);

		this.RegisterAllViewModels();

		RxApp.MainThreadScheduler.Schedule(async () => await NavBar.InitialiseAsync());

		this.WhenAnyValue(x => x.WindowState)
			.Where(state => state != WindowState.Minimized)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(state => UiContext.ApplicationSettings.WindowState = state);

		IsMainContentEnabled =
			this.WhenAnyValue(
					x => x.DialogScreen.IsDialogOpen,
					x => x.CompactDialogScreen.IsDialogOpen,
					(dialogIsOpen, compactIsOpen) => !(dialogIsOpen || compactIsOpen))
				.ObserveOn(RxApp.MainThreadScheduler);

		CurrentWallet =
			this.WhenAnyValue(x => x.MainScreen.CurrentPage)
				.WhereNotNull()
				.OfType<WalletViewModel>();

		IsOobeBackgroundVisible = UiContext.ApplicationSettings.Oobe;

		SearchBar = CreateSearchBar();

		NetworkBadgeName =
			UiContext.ApplicationSettings.Network == Network.Main
			? ""
			: UiContext.ApplicationSettings.Network.Name;

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			if (UiContext.TwoFactorAuthentication.TwoFactorEnabled)
			{
				IsOobeBackgroundVisible = true;
				await UiContext.Navigate().To().VerifyTwoFactoryAuthenticationDialog().GetResultAsync();
				IsOobeBackgroundVisible = false;
			}

			NavBar.Activate();

			if (!UiContext.WalletRepository.HasWallet || UiContext.ApplicationSettings.Oobe)
			{
				IsOobeBackgroundVisible = true;

				await UiContext.Navigate().To().WelcomePage().GetResultAsync();

				if (UiContext.WalletRepository.HasWallet)
				{
					UiContext.ApplicationSettings.Oobe = false;
					IsOobeBackgroundVisible = false;
				}
			}

			await AnnouncementIcon.ShowImportantAnnouncementsAsync();
		});

		// TODO: the reason why this MainViewModel singleton is even needed throughout the codebase is dubious.
		// Also it causes tight coupling which damages testability.
		// We should strive to remove it altogether.
		if (Instance != null)
		{
			throw new InvalidOperationException($"MainViewModel instantiated more than once.");
		}

		Instance = this;
	}

	public IObservable<bool> IsMainContentEnabled { get; }

	public string NetworkBadgeName { get; }

	public IObservable<WalletViewModel> CurrentWallet { get; }

	public TargettedNavigationStack MainScreen { get; }

	public SearchBarViewModel SearchBar { get; }

	public DialogScreenViewModel DialogScreen { get; }
	public DialogScreenViewModel CompactDialogScreen { get; }
	public NavBarViewModel NavBar { get; }
	public StatusIconViewModel StatusIcon { get; }
	public AnnouncementIconViewModel AnnouncementIcon { get; }
	public SettingsPageViewModel SettingsPage { get; }
	public PrivacyModeViewModel PrivacyMode { get; }
	public WalletNotificationsViewModel Notifications { get; }

	public static MainViewModel Instance { get; private set; }

	public bool IsDialogOpen()
	{
		return DialogScreen.IsDialogOpen || CompactDialogScreen.IsDialogOpen;
	}

	public void ShowDialogAlert()
	{
		if (CompactDialogScreen.IsDialogOpen)
		{
			CompactDialogScreen.ShowAlert = false;
			CompactDialogScreen.ShowAlert = true;
			return;
		}

		if (DialogScreen.IsDialogOpen)
		{
			DialogScreen.ShowAlert = false;
			DialogScreen.ShowAlert = true;
			return;
		}
	}

	public void ClearStacks()
	{
		MainScreen.Clear();
		DialogScreen.Clear();
		CompactDialogScreen.Clear();
	}

	public void Initialize()
	{
		UiContext.WalletRepository.Wallets
			.Connect()
			.FilterOnObservable(x => x.Coinjoin.IsRunning)
			.ToCollection()
			.Select(x => x.Count != 0)
			.BindTo(this, x => x.IsCoinJoinActive);

		Notifications.StartListening();

		AnnouncementIcon.Initialize();

		if (UiContext.ApplicationSettings.Network != Network.Main)
		{
			Title += $" - {UiContext.ApplicationSettings.Network}";
		}
	}

	public void ApplyUiConfigWindowState()
	{
		WindowState = UiContext.ApplicationSettings.WindowState;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Same lifecycle as the application. Won't be disposed separately.")]
	private SearchBarViewModel CreateSearchBar()
	{
		// This subject is created to solve the circular dependency between the sources and SearchBarViewModel
		var querySubject = new Subject<string>();

		var source = new CompositeSearchSource(
			new ActionsSearchSource(UiContext, querySubject),
			new SettingsSearchSource(UiContext, querySubject),
			new TransactionsSearchSource(querySubject),
			UiContext.EditableSearchSource);

		var searchBar = new SearchBarViewModel(source);

		var queries = searchBar
			.WhenAnyValue(a => a.SearchText)
			.WhereNotNull();

		UiContext.EditableSearchSource.SetQueries(queries);

		queries
			.Subscribe(querySubject);

		return searchBar;
	}
}
