using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using System.Windows.Input;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class GeneralSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private bool _runOnSystemStartup;
	[AutoNotify] private bool _modifyTorEnabled;

	public GeneralSettingsTabViewModel(UiContext uiContext, IApplicationSettings settings)
	{
		UiContext = uiContext;
		Settings = settings;
		_runOnSystemStartup = settings.RunOnSystemStartup;

		StartupCommand = ReactiveCommand.Create(async () =>
		{
			try
			{
				settings.RunOnSystemStartup = RunOnSystemStartup;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				RunOnSystemStartup = !RunOnSystemStartup;
				await ShowErrorAsync(Title, Resources.CouldNotSaveChange, "");
			}
		});

		var browserList = new List<BrowserTypeDropdownListEnum>
		{
			BrowserTypeDropdownListEnum.SystemDefault
		};

		foreach (var browserType in WebBrowserService.GetAvailableBrowsers())
		{
			if (Enum.TryParse<BrowserTypeDropdownListEnum>(browserType.ToString(), out var result))
			{
				browserList.Add(result);
			}
		}

		this.WhenAnyValue(x => x.UiContext.TwoFactorAuthentication.TwoFactorEnabled)
			.Subscribe(x => ModifyTorEnabled = !x);

		browserList.Add(BrowserTypeDropdownListEnum.Custom);
		BrowserList = browserList;
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand StartupCommand { get; }

	public IEnumerable<FeeDisplayUnit> FeeDisplayUnits =>
		Enum.GetValues(typeof(FeeDisplayUnit)).Cast<FeeDisplayUnit>();

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();

	public IEnumerable<BrowserTypeDropdownListEnum> BrowserList { get; }

	public IEnumerable<DisplayLanguage> DisplayLanguagesList => Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>();
}
