using System.Collections.Generic;
using WalletWasabi.Fluent.SearchBar.Models;

namespace WalletWasabi.Fluent.SearchBar.Interfaces;

public interface ISearchItem
{
	public string Name { get; }
	public string Description { get; }
	public ComposedKey Key { get; }
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; }
	public int Priority { get; }
}
