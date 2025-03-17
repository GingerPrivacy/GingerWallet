using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;

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

		this.WhenAnyValue(
				x => x.UiContext.TwoFactorAuthentication.TwoFactorEnabled,
				x => x.UiContext.ApplicationSettings.Network)
			.Subscribe(x =>
			{
				var (twoFactor, network) = x;
				ModifyTorEnabled = !twoFactor && network != Network.RegTest;
			});

		browserList.Add(BrowserTypeDropdownListEnum.Custom);
		BrowserList = browserList;
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand StartupCommand { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();

	public IEnumerable<BrowserTypeDropdownListEnum> BrowserList { get; }
}
