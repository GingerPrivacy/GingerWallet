using WalletWasabi.Fluent.SearchBar.Interfaces;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

public interface IEditableSearchSource : ISearchSource
{
	void Remove(params ISearchItem[] searchItems);
	void Add(params ISearchItem[] searchItems);
	void SetQueries(IObservable<string> queries);
}
