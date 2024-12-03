namespace WalletWasabi.Announcer;

public class LocalizedAnnouncement
{
	public static readonly LocalizedAnnouncement Empty = new("", "");

	public LocalizedAnnouncement(string local, string content)
	{
		Local = local;

		Title = GetNext(ref content);
		Caption = GetNext(ref content);
		Content = content.Trim('\n') + "\n";
	}

	private static string GetNext(ref string content)
	{
		content = content.Trim('\n');
		int idx = content.IndexOf('\n');
		if (idx < 0)
		{
			idx = content.IndexOf(". ") + 1;
			if (idx == 0)
			{
				idx = content.Length;
			}
		}
		string res = content[0..idx];
		if (++idx >= content.Length) { idx = content.Length; }
		content = content[idx..];
		return res;
	}

	public string Local { get; }
	public string Title { get; }
	public string Caption { get; }
	public string Content { get; }
}
