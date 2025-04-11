using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls.Sorting;

public class SortableItemDesign : ISortableItem
{
	public ICommand SortByDescendingCommand { get; set; } = ReactiveCommand.Create(() => "");
	public ICommand SortByAscendingCommand { get; set; } = ReactiveCommand.Create(() => "");
	public string Name { get; set; } = "";
	public bool IsDescendingActive { get; set; }
	public bool IsAscendingActive { get; set; }
}
