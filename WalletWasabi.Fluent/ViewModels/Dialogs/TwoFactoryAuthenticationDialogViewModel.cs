using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class TwoFactoryAuthenticationDialogViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private string? _twoFactorToken;

	private string? _clientServerId;

	private TwoFactoryAuthenticationDialogViewModel()
	{
		Title = "Two Factor Authentication Setup";

		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				IsBusy = true;
				await UiContext.TwoFactorAuthentication.VerifyAndSaveClientFileAsync(TwoFactorToken!, _clientServerId!);
				Close(result: true);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, "Couldn't verify the token, please see the logs for further information.", $"{ex.Message}");
			}
			finally
			{
				IsBusy = false;
			}
		}, this.WhenAnyValue(x => x.TwoFactorToken).Select(x => !string.IsNullOrEmpty(x) && x.Length == 8));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		this.WhenAnyValue(x => x.TwoFactorToken)
			.Where(x => !string.IsNullOrEmpty(x) && x.Length == 8)
			.Take(1)
			.Do(_ => NextCommand.ExecuteIfCan())
			.Subscribe();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public IObservable<bool[,]>? QrCodeItem { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		if (isInHistory)
		{
			return;
		}

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			try
			{
				IsBusy = true;
				var result = await UiContext.TwoFactorAuthentication.SetupTwoFactorAuthentication();
				_clientServerId = result.ClientServerId;
				QrCodeItem = UiContext.QrCodeGenerator.Generate(result.QrCodeUri);
				this.RaisePropertyChanged(nameof(QrCodeItem));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, "Couldn't verify the token, please see the logs for further information.", "");
			}
			finally
			{
				IsBusy = false;
			}
		});
	}
}
