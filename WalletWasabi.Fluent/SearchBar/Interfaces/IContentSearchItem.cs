namespace WalletWasabi.Fluent.SearchBar.Interfaces;

public interface IContentSearchItem : ISearchItem
{
	object Content { get; }
	public bool IsEnabled { get; }
}
