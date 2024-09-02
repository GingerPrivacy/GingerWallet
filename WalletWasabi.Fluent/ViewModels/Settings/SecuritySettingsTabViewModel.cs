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
using System.Reactive.Disposables;
using WalletWasabi.Services;

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
	[AutoNotify] private bool _twoFactorEnabled;

	public SecuritySettingsTabViewModel(UiContext uiContext, IApplicationSettings settings)
	{
		Settings = settings;

		TwoFactorEnabled = uiContext.TwoFactorAuthenticationModel.TwoFactorEnabled;

		GenerateTwoFactorCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (TwoFactorEnabled)
			{
				var result = await UiContext.Navigate().To().TwoFactoryAuthenticationDialog().GetResultAsync();
				UiContext.ApplicationSettings.ForceRestartNeeded = result;
				TwoFactorEnabled = result;
			}
			else
			{
				UiContext.TwoFactorAuthenticationModel.RemoveTwoFactorAuthentication();
				TwoFactorEnabled = false;
			}
		});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand GenerateTwoFactorCommand { get; set; }
}
