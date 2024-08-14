using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Daemon.Helpers;

namespace WalletWasabi.Fluent.CrashReport.ViewModels;

public class CrashReportWindowViewModel : ViewModelBase
{
	public CrashReportWindowViewModel(SerializableException serializedException)
	{
		SerializedException = serializedException;
		CancelCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: true));
		NextCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: false));

		OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () => await BrowserHelpers.Instance.OpenUrlInPreferredBrowserAsync(Link));

		CopyTraceCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			await ApplicationHelper.SetTextAsync(Trace);
		});
	}

	public SerializableException SerializedException { get; }

	public ICommand OpenGitHubRepoCommand { get; }

	public ICommand NextCommand { get; }

	public ICommand CancelCommand { get; }

	public ICommand CopyTraceCommand { get; }

	public string Caption => $"A problem has occurred and Ginger is unable to continue.";

	public string Link => AboutViewModel.BugReportLink;

	public string Trace => SerializedException.ToString();

	public string Title => "Ginger Wallet has crashed";
}
