using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.TransactionBroadcasting.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class BroadcastTransactionViewModel : RoutableViewModel
{
	public BroadcastTransactionViewModel(SmartTransaction transaction)
	{
		Title = Resources.BroadcastTransaction;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(transaction));

		EnableAutoBusyOn(NextCommand);

		var broadcastInfo = UiContext.TransactionBroadcaster.GetBroadcastInfo(transaction);
		TransactionId = broadcastInfo.TransactionId;
		OutputAmountString = broadcastInfo.OutputAmountString;
		InputAmountString = broadcastInfo.InputAmoutString;
		FeeString = broadcastInfo.FeeString;
		InputCount = broadcastInfo.InputCount;
		OutputCount = broadcastInfo.OutputCount;
	}

	public string? TransactionId { get; set; }

	public string? OutputAmountString { get; set; }

	public string? InputAmountString { get; set; }

	public string FeeString { get; set; }

	public int InputCount { get; set; }

	public int OutputCount { get; set; }

	private async Task OnNextAsync(SmartTransaction transaction)
	{
		try
		{
			await UiContext.TransactionBroadcaster.SendAsync(transaction);
			UiContext.Navigate().To().Success();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToBroadcastTransaction);
		}
	}
}
