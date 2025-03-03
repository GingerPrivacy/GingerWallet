using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;

namespace WalletWasabi.Fluent.SearchBar.ViewModels;

public class SearchItemGroup : IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<ISearchItem> _items;

	public SearchItemGroup(string title, IObservable<IChangeSet<ISearchItem, ComposedKey>> changes)
	{
		Title = title;
		changes
			.Sort(SortExpressionComparer<ISearchItem>.Ascending(x => x.Priority))
			.Bind(out _items)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public string Title { get; }

	public ReadOnlyObservableCollection<ISearchItem> Items => _items;

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
