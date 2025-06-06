using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.General,
	IconName = "info_regular",
	Searchable = true,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true)]
public partial class LegalDocumentsViewModel : RoutableViewModel
{
	[AutoNotify] private string? _content;

	public LegalDocumentsViewModel()
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = true;

		NextCommand = BackCommand;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (isInHistory)
		{
			return;
		}

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			try
			{
				IsBusy = true;
				var document = await UiContext.LegalDocumentsProvider.WaitAndGetLatestDocumentAsync();
				Content = document.Content;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, message: Resources.FailedToGetLegalDocuments, caption: "");
				UiContext.Navigate(CurrentTarget).Back();
			}
			finally
			{
				IsBusy = false;
			}
		});
	}
}
