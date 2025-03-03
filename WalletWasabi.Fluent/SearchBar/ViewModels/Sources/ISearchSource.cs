using DynamicData;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

public interface ISearchSource
{
	IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }
}
