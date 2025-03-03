using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Common.ViewModels;

public class ActivatableViewModel : ViewModelBase
{
	protected virtual void OnActivated(CompositeDisposable disposables)
	{
	}

	public void Activate(CompositeDisposable disposables)
	{
		OnActivated(disposables);
	}
}
