using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Searchable = false,
	NavBarPosition = NavBarPosition.Bottom,
	NavBarSelectionMode = NavBarSelectionMode.Toggle)]
public partial class PrivacyModeViewModel : RoutableViewModel
{
	[AutoNotify] private bool _privacyMode;
	[AutoNotify] private string _iconName;
	[AutoNotify] private string _iconNameFocused = "";

	public PrivacyModeViewModel(ApplicationSettings applicationSettings)
	{
		Title = Resources.DiscreetMode;

		_privacyMode = applicationSettings.PrivacyMode;
		_iconName = GetIcon();

		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.Do(x => applicationSettings.PrivacyMode = x)
			.Subscribe();
	}

	public void Toggle()
	{
		PrivacyMode = !PrivacyMode;
		IconName = GetIcon();
	}

	private string GetIcon()
	{
		return PrivacyMode ? "eye_hide_regular" : "eye_show_regular";
	}
}
