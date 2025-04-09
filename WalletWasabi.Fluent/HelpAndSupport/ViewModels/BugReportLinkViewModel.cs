using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.HelpAndSupport,
	IconName = "bug_regular",
	IsLocalized = true)]
public partial class BugReportLinkViewModel : TriggerCommandViewModel
{
	public BugReportLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.BugReportLink));
	}

	public override ICommand TargetCommand { get; }
}
