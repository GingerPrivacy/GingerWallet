using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Announcement.Models;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.Announcement.ViewModels;

[AppLifetime]
public partial class AnnouncementIconViewModel : ViewModelBase
{
	[AutoNotify] private AnnouncementModel? _selectedAnnouncement;
	[AutoNotify] private bool _hideFlyout;

	public AnnouncementIconViewModel(AnnouncementsModel announcements)
	{
		Announcements = announcements;

		this.WhenAnyValue(x => x.SelectedAnnouncement)
			.WhereNotNull()
			.Do(model =>
			{
				HideFlyout = true;
				UiContext.Navigate().To().AnnouncementDialog(model);
				SelectedAnnouncement = null;
				HideFlyout = false;
			})
			.Subscribe();

		MarkAllAsReadCommand = ReactiveCommand.Create(() => Announcements.MarkAllAsRead());
	}

	public AnnouncementsModel Announcements { get; }

	public ICommand MarkAllAsReadCommand { get; }

	public async Task ShowImportantAnnouncementsAsync()
	{
		await Announcements.WaitUntilIntializedAsync();

		var announcementsToShow = Announcements.List.Where(x => x.IsUnread && x.IsImportant).ToArray();

		foreach (AnnouncementModel announcement in announcementsToShow)
		{
			await UiContext.Navigate().To().AnnouncementDialog(announcement).GetResultAsync();
		}
	}

	public void Initialize()
	{
		Announcements.Initialize();
	}
}
