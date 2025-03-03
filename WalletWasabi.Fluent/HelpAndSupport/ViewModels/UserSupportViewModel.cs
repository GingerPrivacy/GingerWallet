using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.HelpAndSupport,
	IconName = "person_support_regular",
	IsLocalized = true)]
public partial class UserSupportViewModel : TriggerCommandViewModel
{
	private UserSupportViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.UserSupportLink));
	}

	public override ICommand TargetCommand { get; }
}
