using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.SearchBar.Interfaces;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

public static class EditableSearchSourceExtensions
{
	public static void Toggle(this EditableSearchSourceSource searchSource, ISearchItem searchItem, bool isDisplayed)
	{
		if (isDisplayed)
		{
			searchSource.Add(searchItem);
		}
		else
		{
			searchSource.Remove(searchItem);
		}
	}

	public static void Toggle(this EditableSearchSourceSource searchSource, IEnumerable<ISearchItem> searchItems, bool isDisplayed)
	{
		if (isDisplayed)
		{
			searchSource.Add(searchItems.ToArray());
		}
		else
		{
			searchSource.Remove(searchItems.ToArray());
		}
	}
}
