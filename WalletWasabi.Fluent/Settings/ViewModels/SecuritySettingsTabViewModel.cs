using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 4,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class SecuritySettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private bool _twoFactorEnabled;
	[AutoNotify] private bool _modifyTwoFactorEnabled;
	[AutoNotify] private bool _modifyTorEnabled;

	public SecuritySettingsTabViewModel(ApplicationSettings settings)
	{
		Settings = settings;
		TwoFactorEnabled = UiContext.TwoFactorAuthentication.TwoFactorEnabled;

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

		this.WhenAnyValue(
				x => x.UiContext.TwoFactorAuthentication.TwoFactorEnabled,
				x => x.UiContext.ApplicationSettings.Network)
			.Subscribe(x =>
			{
				var (twoFactor, network) = x;
				ModifyTorEnabled = !twoFactor && network != Network.RegTest;
			});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public ApplicationSettings Settings { get; }

	public ICommand GenerateTwoFactorCommand { get; set; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();
}
