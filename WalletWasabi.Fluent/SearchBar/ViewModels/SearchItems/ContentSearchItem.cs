using System.Collections.Generic;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.SearchItems;

public class ContentSearchItem : IContentSearchItem
{
	public ContentSearchItem(object content, string name, string category, IEnumerable<string> keywords, string? icon, bool isEnabled = true)
	{
		Name = name;
		Content = content;
		Category = category;
		Keywords = keywords;
		Icon = icon;
		IsEnabled = isEnabled;
	}

	public string Name { get; }
	public object Content { get; }
	public ComposedKey Key => new(Name);
	public string Description => "";
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; set; }
	public int Priority { get; set; }
	public bool IsEnabled { get; }
}
