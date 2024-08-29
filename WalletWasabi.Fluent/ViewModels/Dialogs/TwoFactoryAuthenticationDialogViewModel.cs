using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Two Factor Authentication Setup", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class TwoFactoryAuthenticationDialogViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private string? _twoFactorToken;
	private string? _clientId;
	private string? _serverSecret;

	private TwoFactoryAuthenticationDialogViewModel()
	{
		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				IsBusy = true;
				await UiContext.TwoFactorAuthenticationModel.VerifyAndSaveClientFileAsync(TwoFactorToken, _clientId!, _serverSecret!);
				Close(result: true);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, "Couldn't verify the token, please see the logs for further information.", "Error occurred.");
			}
			finally
			{
				IsBusy = false;
			}
		}, this.WhenAnyValue(x => x.TwoFactorToken).Select(x => !string.IsNullOrEmpty(x) && x.Length == 8));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public IObservable<bool[,]>? QrCodeItem { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			try
			{
				IsBusy = true;
				var result = await UiContext.TwoFactorAuthenticationModel.SetupTwoFactorAuthentication();
				_clientId = result.ClientId;
				_serverSecret = result.SecretServer;
				QrCodeItem = UiContext.QrCodeGenerator.Generate(result.QrCodeUri);
				this.RaisePropertyChanged(nameof(QrCodeItem));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, "Couldn't verify the token, please see the logs for further information.", "Error occurred.");
			}
			finally
			{
				IsBusy = false;
			}
		});
	}
}
