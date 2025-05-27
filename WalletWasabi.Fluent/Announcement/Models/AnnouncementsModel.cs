using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Announcer;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.Announcement.Models;

[AppLifetime]
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
public class AnnouncementsModel
{
	private readonly ReadOnlyObservableCollection<AnnouncementModel> _list;
	private readonly SourceCache<AnnouncementModel, long> _listCache;

	private AnnouncementManager? _announcementManager;

	public AnnouncementsModel()
	{
		_listCache = new(x => x.OrderNumber);
		_listCache
			.Connect()
			.Sort(SortExpressionComparer<AnnouncementModel>.Descending(x => x.OrderNumber))
			.Bind(out _list)
			.Subscribe();

		HasUnreadAnnouncements = List
			.ToObservableChangeSet(x => x.OrderNumber)
			.AutoRefresh(x => x.IsUnread)
			.Filter(model => model.IsUnread)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		HasImportantUnreadAnnouncements = List
			.ToObservableChangeSet(x => x.OrderNumber)
			.AutoRefresh(x => x.IsUnread)
			.Filter(model => model.IsUnread && model.IsImportant)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		HasAny = this.WhenAnyValue(x => x.List.Count).Select(count => count > 0);
	}

	private TaskCompletionSource InitializedTcs { get; } = new();
	public IObservable<bool> HasImportantUnreadAnnouncements { get; set; }
	public IObservable<bool> HasUnreadAnnouncements { get; set; }
	public IObservable<bool> HasAny { get; set; }
	public ReadOnlyObservableCollection<AnnouncementModel> List => _list;

	public void MarkAllAsRead()
	{
		foreach (var model in List)
		{
			model.IsUnread = false;
		}
	}

	public Task WaitUntilIntializedAsync() => InitializedTcs.Task;

	public void Initialize()
	{
		_announcementManager = Services.HostedServices.Get<AnnouncementManager>();

		Observable
			.FromEventPattern<List<Announcer.Announcement>>(_announcementManager, nameof(_announcementManager.AnnouncementsUpdated))
			.Select(x => x.EventArgs)
			.StartWith(_announcementManager.GetAnnouncement())
			.Do(announcements =>
			{
				_listCache.Clear();
				_listCache.AddOrUpdate(announcements.Select(x => new AnnouncementModel(x)));
			})
			.Subscribe();

		List.ToObservableChangeSet(x => x.OrderNumber)
			.WhenPropertyChanged(x => x.IsUnread, notifyOnInitialValue: false)
			.Where(x => x.Value == false)
			.Select(x => x.Sender)
			.Do(x => _announcementManager.MarkAsRead(x.Announcement))
			.Subscribe();

		InitializedTcs.TrySetResult();
	}
}
