using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class GeneralSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private bool _runOnSystemStartup;

	public GeneralSettingsTabViewModel(ApplicationSettings settings)
	{
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

		browserList.Add(BrowserTypeDropdownListEnum.Custom);
		BrowserList = browserList;
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public ApplicationSettings Settings { get; }

	public ICommand StartupCommand { get; }

	public IEnumerable<BrowserTypeDropdownListEnum> BrowserList { get; }
}
