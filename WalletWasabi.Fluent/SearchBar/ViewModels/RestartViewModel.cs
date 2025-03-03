using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.SearchBar.ViewModels;

public class RestartViewModel : ViewModelBase
{
	public RestartViewModel(string message)
	{
		Message = message;
		RestartCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true));
	}

	public string Message { get; }

	public ICommand RestartCommand { get; }
}
