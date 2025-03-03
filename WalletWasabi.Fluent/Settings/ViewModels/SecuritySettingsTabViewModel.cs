using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
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
