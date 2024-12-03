using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace WalletWasabi.Announcer;

[JsonObject(MemberSerialization.OptIn)]
public partial class Announcement
{
	public Announcement(string id, string publicKey, DateTimeOffset createdAt, int kind, string signature, string content, bool isUnread = true)
	{
		Id = id;
		PublicKey = publicKey;
		CreatedAt = createdAt;
		Kind = kind;
		Signature = signature;
		Content = content;
		IsUnread = isUnread;
		Saved = false;
		IsImportant = false;
		Parse();
		SetLocalized("en");
	}

	public LocalizedAnnouncement? GetLocalized(string local)
	{
		LocalizedAnnouncement? announcement = _localizedAnnouncements.Find(x => x.Local == local);
		announcement ??= _localizedAnnouncements.Find(x => local.StartsWith(x.Local[0..2]));
		return announcement;
	}

	[MemberNotNull(nameof(Localized))]
	public bool SetLocalized(string local)
	{
		var localized = GetLocalized(local);
		Localized = localized ?? LocalizedAnnouncement.Empty;
		return localized is not null;
	}

	internal void Parse()
	{
		OrderNumber = CreatedAt.Ticks;

		string content = Content.Replace("\r\n", "\n");

		var locals = LocalRegex().Matches(content).ToList();
		_localizedAnnouncements.Clear();
		if (locals.Count > 0)
		{
			for (int idx = 0, len = locals.Count; idx < len; idx++)
			{
				Match match = locals[idx];
				int startIndex = match.Index + match.Length;
				int endIndex = idx + 1 < len ? locals[idx + 1].Index : content.Length;
				LocalizedAnnouncement la = new(match.Groups[1].Value, content[startIndex..endIndex]);
				_localizedAnnouncements.Add(la);
			}
			string settings = content[0..locals[0].Index].ToLowerInvariant();
			IsImportant = settings.Contains("[important]", StringComparison.InvariantCulture);
		}
		else
		{
			_localizedAnnouncements.Add(new("en-US", content));
			IsImportant = false;
		}
	}

	[JsonProperty]
	public string Id { get; set; }

	[JsonProperty]
	public string PublicKey { get; set; }

	[JsonProperty]
	public DateTimeOffset CreatedAt { get; set; }

	[JsonProperty]
	public int Kind { get; set; }

	[JsonProperty]
	public string Signature { get; set; }

	[JsonProperty]
	public string Content { get; set; }

	[JsonProperty]
	public bool IsUnread { get; set; }

	public bool Saved { get; set; }
	public string FileName => $"{Id}.json";

	public bool IsImportant { get; private set; }
	public long OrderNumber { get; private set; }

	public LocalizedAnnouncement Localized { get; private set; }

	private List<LocalizedAnnouncement> _localizedAnnouncements = new();

	[GeneratedRegex("""^\[([a-z][a-z]-[A-Z][A-Z])\][ \t]*$""", RegexOptions.Multiline)]
	private static partial Regex LocalRegex();
}
