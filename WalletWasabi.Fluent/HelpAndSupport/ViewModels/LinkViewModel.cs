using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

public class LinkViewModel : ViewModelBase
{
	public LinkViewModel()
	{
		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.FileSystem.OpenBrowserAsync(link));
		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.Clipboard.SetTextAsync(link));
	}

	public string? Link { get; set; }

	public string? Description { get; set; }

	public bool IsClickable { get; set; }

	public ICommand OpenBrowserCommand { get; }

	public ICommand CopyLinkCommand { get; }
}
