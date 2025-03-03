using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

public class CompositeSearchSource : ISearchSource
{
	private readonly ISearchSource[] _sources;

	public CompositeSearchSource(params ISearchSource[] sources)
	{
		_sources = sources;
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes => _sources.Select(r => r.Changes).Merge();
}
