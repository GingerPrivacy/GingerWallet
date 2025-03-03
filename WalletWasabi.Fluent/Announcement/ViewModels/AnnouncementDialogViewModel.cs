using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Announcement.Models;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;

namespace WalletWasabi.Fluent.Announcement.ViewModels;

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
