using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;
using WalletWasabi.Fluent.SearchBar.Models.Settings;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.SearchItems;

public class SearchItemNode<TObject, TProperty> : ReactiveObject, IContentSearchItem where TObject : class, INotifyPropertyChanged
{
	private readonly EditableSearchSourceSource _editableSearchSource;
	private readonly NestedItemConfiguration<TProperty>[] _nestedItems;
	private readonly Setting<TObject, TProperty> _setting;
	private readonly CompositeDisposable _disposables = new();

	public SearchItemNode(EditableSearchSourceSource editableSearchSource, Setting<TObject, TProperty> setting, string name, string category, IEnumerable<string> keywords, string? icon, bool isDefault, bool isEnabled, params NestedItemConfiguration<TProperty>[] nestedItems)
	{
		_editableSearchSource = editableSearchSource;
		_setting = setting;
		_nestedItems = nestedItems;
		Name = name;
		Content = setting;
		Category = category;
		Keywords = keywords;
		Icon = icon;
		IsDefault = isDefault;
		IsEnabled = isEnabled;
		this.WhenAnyValue(item => item._setting.Value)
			.Do(AddOrRemoveNestedItems)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public object Content { get; }
	public string Name { get; }
	public ComposedKey Key => new(Name);
	public string Description => "";
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; }
	public int Priority { get; set; }
	public bool IsEnabled { get; }

	public void Dispose() => _disposables.Dispose();

	private void AddOrRemoveNestedItems(TProperty? property)
	{
		foreach (var nestedItem in _nestedItems)
		{
			var isDisplayed = nestedItem.IsDisplayed(property);
			_editableSearchSource.Toggle(nestedItem.Item, isDisplayed);
		}
	}
}
