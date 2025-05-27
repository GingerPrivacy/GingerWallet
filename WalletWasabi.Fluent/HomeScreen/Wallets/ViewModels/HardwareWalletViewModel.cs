using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;

[AppLifetime]
[NavigationMetaData(NavigationTarget = NavigationTarget.HomeScreen)]
public partial class HardwareWalletViewModel : WalletViewModel
{
	internal HardwareWalletViewModel(WalletModel walletModel, Wallet wallet) : base(walletModel, wallet)
	{
		BroadcastPsbtCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				var file = await FileDialogHelper.OpenFileAsync(Resources.ImportTransactionFileDialogTitle, new[] { "psbt", "txn", "*" });
				if (file is { })
				{
					var path = file.Path.AbsolutePath;
					var txn = await walletModel.Transactions.LoadFromFileAsync(path);
					UiContext.Navigate().To().BroadcastTransaction(txn);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToLoadTransaction);
			}
		});
	}
}
