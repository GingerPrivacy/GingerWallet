using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.HelpAndSupport,
	IconName = "book_question_mark_regular",
	IsLocalized = true)]
public partial class DocsLinkViewModel : TriggerCommandViewModel
{
	public DocsLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.FAQLink));
	}

	public override ICommand TargetCommand { get; }
}
