using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Status.ViewModels;

[AppLifetime]
public partial class StatusIconViewModel : ViewModelBase
{
	[AutoNotify] private string? _versionText;

	public StatusIconViewModel(UiContext uiContext)
	{
		UiContext = uiContext;
		HealthMonitor = uiContext.HealthMonitor;

		ManualUpdateCommand = ReactiveCommand.CreateFromTask(() => UiContext.FileSystem.OpenBrowserAsync(UiConstants.DownloadLink));
		UpdateCommand = ReactiveCommand.Create(
			() =>
			{
				UiContext.ApplicationSettings.DoUpdateOnClose = true;
				AppLifetimeHelper.Shutdown();
			});

		AskMeLaterCommand = ReactiveCommand.Create(() => HealthMonitor.CheckForUpdates = false);

		OpenTorStatusSiteCommand = ReactiveCommand.CreateFromTask(() => UiContext.FileSystem.OpenBrowserAsync(UiConstants.TorStatusLink));

		this.WhenAnyValue(
				x => x.HealthMonitor.UpdateAvailable,
				x => x.HealthMonitor.CriticalUpdateAvailable,
				x => x.HealthMonitor.IsReadyToInstall,
				x => x.HealthMonitor.ClientVersion,
				(updateAvailable, criticalUpdateAvailable, isReadyToInstall, clientVersion) =>
					(updateAvailable || criticalUpdateAvailable || isReadyToInstall) && clientVersion != null)
			.Select(_ => GetVersionText())
			.BindTo<string, StatusIconViewModel, string>(this, x => x.VersionText);
	}

	public IHealthMonitor HealthMonitor { get; }

	public ICommand OpenTorStatusSiteCommand { get; }

	public ICommand UpdateCommand { get; }

	public ICommand ManualUpdateCommand { get; }

	public ICommand AskMeLaterCommand { get; }

	public string BitcoinCoreName => Constants.BuiltinBitcoinNodeName;

	private string GetVersionText()
	{
		if (HealthMonitor.CriticalUpdateAvailable)
		{
			return Resources.CriticalUpdateRequired;
		}
		else if (HealthMonitor.IsReadyToInstall)
		{
			return Resources.VersionReadyToInstall.SafeInject(HealthMonitor.ClientVersion);
		}
		else if (HealthMonitor.UpdateAvailable)
		{
			return Resources.VersionAvailable.SafeInject(HealthMonitor.ClientVersion);
		}

		return string.Empty;
	}
}
