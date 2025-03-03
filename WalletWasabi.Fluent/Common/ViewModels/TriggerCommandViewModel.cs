using System.Windows.Input;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Common.ViewModels;

public abstract class TriggerCommandViewModel : RoutableViewModel
{
	public abstract ICommand TargetCommand { get; }
}
