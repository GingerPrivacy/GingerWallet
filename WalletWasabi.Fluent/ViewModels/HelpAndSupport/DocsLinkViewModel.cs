using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "User Guide/FAQ",
	Caption = "Open Ginger Wallet's FAQ website",
	Order = 2,
	Category = "Help & Support",
	Keywords =
	[
		"User", "Support", "Website", "Docs", "Documentation", "Guide"
	],
	IconName = "book_question_mark_regular")]
public partial class DocsLinkViewModel : TriggerCommandViewModel
{
	private DocsLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.FAQLink));
	}

	public override ICommand TargetCommand { get; }
}
