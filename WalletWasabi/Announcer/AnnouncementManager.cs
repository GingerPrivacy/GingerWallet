using Newtonsoft.Json;
using NNostr.Client;
using NNostr.Client.Protocols;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;
using NostrExtensions = WalletWasabi.Nostr.NostrExtensions;

namespace WalletWasabi.Announcer;

public class AnnouncementManager : PeriodicRunner
{
	public event EventHandler<List<Announcement>>? AnnouncementsUpdated;

	private DateTimeOffset _startedAt = DateTimeOffset.Now;
	private TimeSpan _silentPeriod = TimeSpan.FromDays(1);
	private bool _isFirstRun;

	public AnnouncementManager(string dataDir, TimeSpan period, DisplayLanguage local, string extraNostrKey, WasabiHttpClientFactory httpClientFactory, bool isFirstRun) : base(period)
	{
		string publicKey = "npub1ag3rdayfaxgywnwpdkv75mppcqf46nk9he4f4f473pd60rcwdcusw75gl5";

		_local = local.GetDescription() ?? "en-US";
		_httpClientFactory = httpClientFactory;
		_nostrSubscriptionID = "";
		_isFirstRun = isFirstRun;

		string defaultPubKeyHex = NIP19.FromNIP19Npub(publicKey).ToHex();
		string extraNostrKeyHex = "";
		if (extraNostrKey.Length > 0)
		{
			try
			{
				extraNostrKeyHex = NIP19.FromNIP19Npub(extraNostrKey).ToHex();
			}
			catch (Exception)
			{
				Logger.LogInfo($"Failed to convert the Nostr key {extraNostrKey} to hex format.");
			}
		}

		_nostrPublicKeyHex = extraNostrKeyHex.Length > 0 ? [defaultPubKeyHex, extraNostrKeyHex] : [defaultPubKeyHex];

		SavePeriod = TimeSpan.FromDays(30);
		AnnouncementsPath = Path.Combine(dataDir, "Announcements");

		if (!Directory.Exists(AnnouncementsPath))
		{
			Directory.CreateDirectory(AnnouncementsPath);
		}

		LoadAnnouncements();
	}

	public TimeSpan SavePeriod { get; }

	private string _local;
	private bool _announcementsChanged = false;
	private Dictionary<string, Announcement> _announcementDictionary = new();

	private List<Announcement> Announcements { get; } = new();

	public string AnnouncementsPath { get; }

	private WasabiHttpClientFactory _httpClientFactory;

	public void MarkAsRead(Announcement announcement)
	{
		announcement.IsUnread = false;
		SaveFile(announcement);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		await CheckNostrConnectionAsync(cancellationToken).ConfigureAwait(false);

		bool fireUpdate = false;
		lock (_announcementDictionary)
		{
			DateTimeOffset endOfLife = DateTimeOffset.UtcNow - SavePeriod;
			var toRemove = _announcementDictionary.Values.Where(x => x.CreatedAt < endOfLife).ToList();
			if (toRemove.Count > 0)
			{
				foreach (var announcement in toRemove)
				{
					RemoveFile(announcement);
					_announcementDictionary.Remove(announcement.Id);
				}
				_announcementsChanged = true;
			}
			if (_announcementsChanged)
			{
				Announcements.Clear();
				foreach (var announcement in _announcementDictionary.Values)
				{
					if (announcement.SetLocalized(_local))
					{
						Announcements.Add(announcement);
					}
				}
				_announcementsChanged = false;
				fireUpdate = true;
			}
			foreach (var announcement in _announcementDictionary.Values)
			{
				if (!announcement.Saved)
				{
					if (_isFirstRun && announcement.CreatedAt < _startedAt - _silentPeriod)
					{
						announcement.IsUnread = false;
					}
					SaveFile(announcement);
				}
			}
		}

		if (fireUpdate)
		{
			AnnouncementsUpdated.SafeInvoke(this, Announcements.ToList());
		}
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		if (_nostrClient is not null)
		{
			try
			{
				_nostrClient.Disconnect();
			}
			catch (Exception)
			{
			}

			_nostrClient.Dispose();
			_nostrClient = null;
		}
		return base.StopAsync(cancellationToken);
	}

	public List<Announcement> GetAnnouncement()
	{
		lock (_announcementDictionary)
		{
			return Announcements.ToList();
		}
	}

	private void LoadAnnouncements()
	{
		List<Announcement> list = new();
		var jsonFiles = Directory.GetFiles(AnnouncementsPath, "*.json");
		foreach (var file in jsonFiles)
		{
			try
			{
				var fileContent = File.ReadAllText(file);

				if (JsonConvert.DeserializeObject<Announcement>(fileContent) is { } announcement)
				{
					announcement.Saved = true;
					list.Add(announcement);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
		lock (_announcementDictionary)
		{
			foreach (var announcement in list)
			{
				_announcementDictionary[announcement.Id] = announcement;

				if (announcement.SetLocalized(_local))
				{
					Announcements.Add(announcement);
				}
			}

			_announcementsChanged = true;
		}
	}

	private void RemoveFile(Announcement announcement)
	{
		var filePath = Path.Combine(AnnouncementsPath, announcement.FileName);
		if (File.Exists(filePath))
		{
			try
			{
				File.Delete(filePath);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}

	private void SaveFile(Announcement announcement)
	{
		lock (announcement)
		{
			try
			{
				var jsonContent = JsonConvert.SerializeObject(announcement, Formatting.Indented);
				var filePath = Path.Combine(AnnouncementsPath, announcement.FileName);
				File.WriteAllText(filePath, jsonContent);
				announcement.Saved = true;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}

	private INostrClient? _nostrClient;
	private string _nostrSubscriptionID;
	private string[] _nostrPublicKeyHex;

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId == _nostrSubscriptionID)
		{
			lock (_announcementDictionary)
			{
				foreach (var nostrEvent in args.events)
				{
					var createdAt = nostrEvent.CreatedAt ?? DateTimeOffset.UtcNow;
					if (nostrEvent.Kind != 1 || !_nostrPublicKeyHex.Contains(nostrEvent.PublicKey) || createdAt < DateTimeOffset.UtcNow - SavePeriod)
					{
						continue;
					}

					if (!_announcementDictionary.TryGetValue(nostrEvent.Id, out var announcement))
					{
						announcement = new Announcement(nostrEvent.Id, nostrEvent.PublicKey, createdAt, nostrEvent.Kind, nostrEvent.Signature, nostrEvent.Content ?? "");
						_announcementDictionary[announcement.Id] = announcement;
						_announcementsChanged = true;
					}
					else
					{
						if (nostrEvent.PublicKey != announcement.PublicKey || (nostrEvent.CreatedAt is not null && nostrEvent.CreatedAt != announcement.CreatedAt) || nostrEvent.Kind != announcement.Kind
							|| nostrEvent.Signature != announcement.Signature || (nostrEvent.Content ?? "") != announcement.Content)
						{
							announcement.PublicKey = nostrEvent.PublicKey;
							announcement.CreatedAt = nostrEvent.CreatedAt ?? announcement.CreatedAt;
							announcement.Kind = nostrEvent.Kind;
							announcement.Signature = nostrEvent.Signature;
							announcement.Content = nostrEvent.Content ?? "";
							announcement.Saved = false;
							announcement.Parse();
							_announcementsChanged = true;
						}
					}
				}
			}
		}
	}

	private async Task CheckNostrConnectionAsync(CancellationToken cancellationToken)
	{
		string[] relayUrl = ["wss://relay.primal.net", "wss://nos.lol", "wss://relay.damus.io"];

		if (_nostrClient is null)
		{
			try
			{
				Uri[] uris = relayUrl.Select(x => new Uri(x)).ToArray();
				_nostrClient = NostrExtensions.Create(uris, _httpClientFactory.IsTorEnabled ? _httpClientFactory.TorEndpoint : null);
				_nostrClient.EventsReceived += OnNostrEventsReceived;

				await _nostrClient.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);

				_nostrSubscriptionID = Guid.NewGuid().ToString();
				await _nostrClient.CreateSubscription(_nostrSubscriptionID, [new() { Kinds = [1], Authors = _nostrPublicKeyHex, Since = DateTimeOffset.UtcNow - SavePeriod }], cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Failed to connect to Nostr: {ex.Message}");
				try
				{
					_nostrClient?.Dispose();
				}
				catch (Exception)
				{
				}
				_nostrClient = null;
			}
		}
	}
}
