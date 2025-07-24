using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public interface IUICommand
{
	public string? Key { get; }
	public string Name { get; }
	public object Icon { get; }
	public ICommand? Command { get; }
	public bool IsDefault { get; set; }
}
