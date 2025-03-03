namespace WalletWasabi.Fluent.Announcement.Models;

public partial class AnnouncementModel
{
	[AutoNotify] private bool _isUnread;

	public AnnouncementModel(Announcer.Announcement announcement)
	{
		Announcement = announcement;
		IsUnread = announcement.IsUnread;
	}

	public Announcer.Announcement Announcement { get; }

	public long OrderNumber => Announcement.OrderNumber;
	public string Title => Announcement.Localized.Title;
	public string Caption => Announcement.Localized.Caption;
	public bool IsImportant => Announcement.IsImportant;
	public string MarkdownText => Announcement.Localized.Content;
}
