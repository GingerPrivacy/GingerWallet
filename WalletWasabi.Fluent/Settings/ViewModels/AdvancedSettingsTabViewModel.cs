using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class AdvancedSettingsTabViewModel : RoutableViewModel
{
	public AdvancedSettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }
}
