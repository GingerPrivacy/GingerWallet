using System.Threading.Tasks;

namespace WalletWasabi.Fluent.SearchBar.Interfaces;

public interface IActionableItem : ISearchItem
{
	Func<Task> Activate { get; }
}
