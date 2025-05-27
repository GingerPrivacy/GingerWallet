using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.SearchBar.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.General,
	IconName = "nav_settings_24_regular",
	IconNameFocused = "nav_settings_24_filled",
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarSelectionMode = NavBarSelectionMode.Button,
	IsLocalized = true)]
public partial class SettingsPageViewModel : RoutableViewModel
{
	[AutoNotify] private bool _isModified;
	[AutoNotify] private int _selectedTab;

	private bool _isDisplayed;

	public SettingsPageViewModel()
	{
		_selectedTab = 0;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		GeneralSettingsTab = new GeneralSettingsTabViewModel(UiContext.ApplicationSettings);
		AppearanceSettingsTab = new AppearanceSettingsTabViewModel(UiContext.ApplicationSettings);
		BitcoinTabSettings = new BitcoinTabSettingsViewModel(UiContext.ApplicationSettings);
		AdvancedSettingsTab = new AdvancedSettingsTabViewModel(UiContext.ApplicationSettings);
		SecuritySettingsTab = new SecuritySettingsTabViewModel(UiContext.ApplicationSettings);

		RestartCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true));
		NextCommand = CancelCommand;

		this.WhenAnyValue(x => x.UiContext.ApplicationSettings.DarkModeEnabled)
			.Skip(1)
			.Subscribe(ChangeTheme);

		// Show restart message when needed
		UiContext.ApplicationSettings.IsRestartNeeded
									 .BindTo(this, x => x.IsModified);

		// Show restart notification when needed only if this page is not active.
		UiContext.ApplicationSettings.IsRestartNeeded
				 .Where(x => x && !IsActive && !_isDisplayed && !UiContext.ApplicationSettings.Oobe)
				 .Do(_ => NotificationHelpers.Show(new RestartViewModel(Resources.ApplyNewSettingRestart)))
				 .Subscribe();

		OpenSecurityTabCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			SelectedTab = 4;
			await Activate();
		});
	}

	public bool IsReadOnly => UiContext.ApplicationSettings.IsOverridden;

	public ICommand OpenSecurityTabCommand { get; }

	public ICommand RestartCommand { get; }

	public GeneralSettingsTabViewModel GeneralSettingsTab { get; }
	public AppearanceSettingsTabViewModel AppearanceSettingsTab { get; }
	public BitcoinTabSettingsViewModel BitcoinTabSettings { get; }
	public AdvancedSettingsTabViewModel AdvancedSettingsTab { get; }
	public SecuritySettingsTabViewModel SecuritySettingsTab { get; }

	public Task Activate()
	{
		UiContext.Navigate().Navigate(DefaultTarget).To(this);
		return Task.CompletedTask;
	}

	private void ChangeTheme(bool isDark)
	{
		RxApp.MainThreadScheduler.Schedule(() => ThemeHelper.ApplyTheme(isDark ? Theme.Dark : Theme.Light));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_isDisplayed = true;
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		_isDisplayed = false;
	}
}
