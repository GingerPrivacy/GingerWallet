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
			"Security", "Settings", "2FA", "Two-Factor", "Authentication", "Two", "Factor"
	},
	IconName = "settings_general_regular")]
public partial class SecuritySettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private bool _twoFactorEnabled;
	[AutoNotify] private bool _modifyTwoFactorEnabled;

	public SecuritySettingsTabViewModel(UiContext uiContext, IApplicationSettings settings)
	{
		UiContext = uiContext;
		Settings = settings;
		TwoFactorEnabled = uiContext.TwoFactorAuthentication.TwoFactorEnabled;

		GenerateTwoFactorCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (TwoFactorEnabled)
			{
				var result = await UiContext.Navigate().To().TwoFactoryAuthenticationDialog().GetResultAsync();
				TwoFactorEnabled = result;
			}
			else
			{
				UiContext.TwoFactorAuthentication.RemoveTwoFactorAuthentication();
				TwoFactorEnabled = false;
			}
		});

		this.WhenAnyValue(x => x.Settings.UseTor)
			.Subscribe(x => ModifyTwoFactorEnabled = x != TorMode.Disabled && Settings.GetTorStartupMode() != TorMode.Disabled);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand GenerateTwoFactorCommand { get; set; }
}
