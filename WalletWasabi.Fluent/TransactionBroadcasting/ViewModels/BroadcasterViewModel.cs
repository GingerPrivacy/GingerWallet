using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.TransactionBroadcasting.ViewModels;

[NavigationMetaData(
	IconName = "live_regular",
	Order = 5,
	Category = SearchCategory.General,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true)]
public partial class BroadcasterViewModel : TriggerCommandViewModel
{
	public override ICommand TargetCommand => ReactiveCommand.CreateFromTask(async () =>
	{
		var dialogResult = await UiContext.Navigate().To().LoadTransaction().GetResultAsync();

		if (dialogResult is { } transaction)
		{
			UiContext.Navigate().To().BroadcastTransaction(transaction);
		}
	});
}
