using System;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.TrezorSuite;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 5,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = false)]
public partial class TrezorSuiteSettingsTabViewModel : RoutableViewModel
{
	private readonly TrezorSuiteIntegrationService _integrationService;
	private string? _lastConfigurationError;

	[AutoNotify] private string _suiteExecutablePath = "";
	[AutoNotify] private string _suiteStatus = "Checking…";
	[AutoNotify] private string _suiteVersion = "Unknown";
	[AutoNotify] private string _backendStatus = "Checking…";
	[AutoNotify] private string _configurationStatus = "Checking…";
	[AutoNotify] private string _lastVerified = "Never";
	[AutoNotify] private string _statusMessage = "Checking Trezor Suite and Ginger backend…";
	[AutoNotify] private bool _isConfigured;
	[AutoNotify] private bool _canRestore;
	[AutoNotify] private string _configureButtonText = "Configure and open Trezor Suite";
	[AutoNotify] private string _targetAnonymity = "3";

	public TrezorSuiteSettingsTabViewModel()
		: this(new TrezorSuiteIntegrationService(Services.DataDir))
	{
	}

	internal TrezorSuiteSettingsTabViewModel(TrezorSuiteIntegrationService integrationService)
	{
		_integrationService = integrationService;
		if (Services.IsInitialized)
		{
			TargetAnonymity = Services.UiConfig.TrezorSuiteTargetAnonymity.ToString(CultureInfo.InvariantCulture);
		}

		RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
		ChooseExecutableCommand = ReactiveCommand.CreateFromTask(ChooseExecutableAsync);
		ConfigureAndLaunchCommand = ReactiveCommand.CreateFromTask(ConfigureAndLaunchAsync);
		LaunchCommand = ReactiveCommand.CreateFromTask(LaunchAsync);
		RestoreCommand = ReactiveCommand.CreateFromTask(RestoreAsync);
		OpenLogsCommand = ReactiveCommand.CreateFromTask(OpenLogsAsync);

		EnableAutoBusyOn(
			RefreshCommand,
			ChooseExecutableCommand,
			ConfigureAndLaunchCommand,
			LaunchCommand,
			RestoreCommand,
			OpenLogsCommand);

		RefreshCommand.Execute().Subscribe();
	}

	public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
	public ReactiveCommand<Unit, Unit> ChooseExecutableCommand { get; }
	public ReactiveCommand<Unit, Unit> ConfigureAndLaunchCommand { get; }
	public ReactiveCommand<Unit, Unit> LaunchCommand { get; }
	public ReactiveCommand<Unit, Unit> RestoreCommand { get; }
	public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
	public string LogDirectoryPath => _integrationService.LogDirectoryPath;

	private async Task RefreshAsync()
	{
		try
		{
			ApplyStatus(await _integrationService.GetStatusAsync(SuiteExecutablePath));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			StatusMessage = $"Status check failed: {ex.Message}";
		}
	}

	private async Task ChooseExecutableAsync()
	{
		var file = await FileDialogHelper.OpenFileAsync("Select the Trezor Suite executable", ["*"]);
		if (file is null)
		{
			return;
		}

		SuiteExecutablePath = file.Path.LocalPath;
		await RefreshAsync();
	}

	private async Task ConfigureAndLaunchAsync()
	{
		try
		{
			_lastConfigurationError = null;
			if (!int.TryParse(TargetAnonymity, NumberStyles.None, CultureInfo.InvariantCulture, out var target) || target is < 2 or > 100)
			{
				throw new TrezorSuiteIntegrationException("Enter a whole-number privacy target from 2 to 100.");
			}
			if (Services.IsInitialized)
			{
				Services.UiConfig.TrezorSuiteTargetAnonymity = target;
				Services.UiConfig.ToFile();
			}
			StatusMessage = "Configuring Trezor Suite for Ginger CoinJoin…";
			ApplyStatus(await _integrationService.ConfigureAndLaunchAsync(SuiteExecutablePath, targetAnonymity: target));
		}
		catch (Exception ex)
		{
			_lastConfigurationError = ex.Message;
			ConfigurationStatus = "Configuration failed";
			await HandleOperationErrorAsync(ex);
		}
	}

	private async Task LaunchAsync()
	{
		try
		{
			_integrationService.Launch(SuiteExecutablePath);
			StatusMessage = "Trezor Suite opened.";
		}
		catch (Exception ex)
		{
			await HandleOperationErrorAsync(ex);
		}
	}

	private async Task RestoreAsync()
	{
		try
		{
			StatusMessage = "Restoring the original Trezor Suite configuration…";
			ApplyStatus(await _integrationService.RestoreAndLaunchAsync(SuiteExecutablePath));
			_lastConfigurationError = null;
			StatusMessage = "The original Trezor Suite configuration was restored and Suite was opened.";
		}
		catch (Exception ex)
		{
			await HandleOperationErrorAsync(ex);
		}
	}

	private async Task OpenLogsAsync()
	{
		try
		{
			Directory.CreateDirectory(LogDirectoryPath);
			UiContext.FileSystem.OpenFolderInFileExplorer(LogDirectoryPath);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Trezor Suite logs", ex.Message, "Ginger could not open the Trezor Suite log folder.");
		}
	}

	private void ApplyStatus(TrezorSuiteIntegrationStatus status)
	{
		if (!string.IsNullOrWhiteSpace(status.ExecutablePath))
		{
			SuiteExecutablePath = status.ExecutablePath;
		}

		SuiteStatus = status.IsInstalled ? "Installed" : "Not found";
		SuiteVersion = status.SuiteVersion ?? "Unknown";
		BackendStatus = status.IsBackendAvailable ? "Online" : "Unavailable";
		ConfigurationStatus = status.IsConfigured ? "Configured for Ginger" : "Not configured";
		LastVerified = status.LastVerifiedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "Never";
		IsConfigured = status.IsConfigured;
		CanRestore = status.HasBackup;
		ConfigureButtonText = status.IsConfigured ? "Repair and open Trezor Suite" : "Configure and open Trezor Suite";
		StatusMessage = status.Message;

		if (_lastConfigurationError is { } error)
		{
			ConfigurationStatus = "Configuration failed";
			StatusMessage = $"Last configuration attempt failed: {error}";
		}
	}

	private async Task HandleOperationErrorAsync(Exception exception)
	{
		Logger.LogError(exception);
		StatusMessage = exception.Message;
		await ShowErrorAsync("Trezor Suite", exception.Message, "Ginger could not configure Trezor Suite.");
	}
}
