using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.TransactionBroadcasting.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class LoadTransactionViewModel : DialogViewModelBase<SmartTransaction?>
{
	[AutoNotify] private SmartTransaction? _finalTransaction;

	public LoadTransactionViewModel()
	{
		Title = Resources.BroadcastTransaction;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		this.WhenAnyValue(x => x.FinalTransaction)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(finalTransaction => Close(result: finalTransaction));

		ImportTransactionCommand = ReactiveCommand.CreateFromTask(OnImportTransactionAsync, outputScheduler: RxApp.MainThreadScheduler);

		PasteCommand = ReactiveCommand.CreateFromTask(OnPasteAsync);
	}

	public ICommand PasteCommand { get; }

	public ICommand ImportTransactionCommand { get; }

	private async Task OnImportTransactionAsync()
	{
		try
		{
			var file = await FileDialogHelper.OpenFileAsync(Resources.ImportTransactionFileDialogTitle, new[] { "psbt", "txn", "*" });
			if (file is { })
			{
				var filePath = file.Path.AbsolutePath;
				FinalTransaction = await UiContext.TransactionBroadcaster.LoadFromFileAsync(filePath);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToLoadTransaction);
		}
	}

	private async Task OnPasteAsync()
	{
		try
		{
			var textToPaste = await UiContext.Clipboard.GetTextAsync();

			if (string.IsNullOrWhiteSpace(textToPaste))
			{
				throw new InvalidDataException(Resources.ClipboardEmpty);
			}

			FinalTransaction = UiContext.TransactionBroadcaster.Parse(textToPaste);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToPasteTransaction);
		}
	}
}
