using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Settings",
	Caption = "Manage appearance, privacy and other settings",
	Order = 1,
	Category = "General",
	Keywords = new[] { "Settings", "General", "User", "Interface", "Advanced" },
	IconName = "nav_settings_24_regular",
	IconNameFocused = "nav_settings_24_filled",
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavigationTarget = NavigationTarget.DialogScreen,
	NavBarSelectionMode = NavBarSelectionMode.Button)]
public partial class SettingsPageViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private bool _isModified;
	[AutoNotify] private int _selectedTab;

	private bool _isDisplayed;

	public SettingsPageViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		_selectedTab = 0;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		GeneralSettingsTab = new GeneralSettingsTabViewModel(UiContext, UiContext.ApplicationSettings);
		BitcoinTabSettings = new BitcoinTabSettingsViewModel(UiContext.ApplicationSettings);
		AdvancedSettingsTab = new AdvancedSettingsTabViewModel(UiContext.ApplicationSettings);
		SecuritySettingsTab = new SecuritySettingsTabViewModel(UiContext, UiContext.ApplicationSettings);

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
				 .Where(x => x && !IsActive && !_isDisplayed)
				 .Do(_ => NotificationHelpers.Show(new RestartViewModel("To apply the new setting, Ginger Wallet needs to be restarted")))
				 .Subscribe();

		OpenSecurityTabCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			SelectedTab = 3;
			await Activate();
		});
	}

	public bool IsReadOnly => UiContext.ApplicationSettings.IsOverridden;

	public ICommand OpenSecurityTabCommand { get; }

	public ICommand RestartCommand { get; }

	public GeneralSettingsTabViewModel GeneralSettingsTab { get; }
	public BitcoinTabSettingsViewModel BitcoinTabSettings { get; }
	public AdvancedSettingsTabViewModel AdvancedSettingsTab { get; }

	public SecuritySettingsTabViewModel SecuritySettingsTab { get; }

	public async Task Activate()
	{
		await NavigateDialogAsync(this);
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
