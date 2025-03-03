using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Announcement;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class AnnouncementDialogViewModel : DialogViewModelBase<Unit>
{
	public AnnouncementDialogViewModel(AnnouncementModel announcement)
	{
		Announcement = announcement;

		NextCommand = ReactiveCommand.Create(() => Close());
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public AnnouncementModel Announcement { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Announcement.IsUnread = false;
	}
}
