using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using System.Windows.Input;
using Gma.QrCodeNet.Encoding;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "Security",
	Caption = "Manage security settings",
	Order = 3,
	Category = "Settings",
	Keywords = new[]
	{
			"Security", "Settings", "2FA"
	},
	IconName = "settings_general_regular")]
public partial class SecuritySettingsTabViewModel : RoutableViewModel
{
	private SecuritySettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		GenerateTwoFactorCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!Settings.TwoFactorEnabled)
			{
				var result = await UiContext.Navigate().To().TwoFactoryAuthenticationDialog().GetResultAsync();
				Settings.TwoFactorEnabled = result;
			}
			else
			{
				UiContext.TwoFactorAuthenticationModel.RemoveTwoFactorAuthentication();
				Settings.TwoFactorEnabled = false;
			}
		});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand GenerateTwoFactorCommand { get; set; }
}
