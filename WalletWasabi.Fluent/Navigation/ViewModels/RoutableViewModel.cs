using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Navigation.Interfaces;

namespace WalletWasabi.Fluent.Navigation.ViewModels;

public abstract partial class RoutableViewModel : ViewModelBase, INavigatable
{
	private CompositeDisposable? _currentDisposable;

	[AutoNotify] private string _title = "";
	[AutoNotify] private string? _caption;
	[AutoNotify] private string[]? _keywords;
	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _enableCancelOnPressed;
	[AutoNotify] private bool _enableCancelOnEscape;
	[AutoNotify] private bool _enableBack;
	[AutoNotify] private bool _enableCancel;
	[AutoNotify] private bool _isActive;

	protected RoutableViewModel()
	{
		BackCommand = ReactiveCommand.Create(() => UiContext.Navigate(CurrentTarget).Back(), this.WhenAnyValue(model => model.IsBusy, b => !b));
		CancelCommand = ReactiveCommand.Create(() => UiContext.Navigate(CurrentTarget).Clear());
	}

	public NavigationTarget CurrentTarget { get; internal set; }

	public virtual NavigationTarget DefaultTarget => NavigationTarget.Unspecified;

	public ICommand? NextCommand { get; protected set; }

	public ICommand? SkipCommand { get; protected set; }

	public ICommand BackCommand { get; protected set; }

	public ICommand CancelCommand { get; protected set; }

	private void DoNavigateTo(bool isInHistory)
	{
		if (_currentDisposable is { })
		{
			throw new Exception("Can't navigate to something that has already been navigated to.");
		}

		_currentDisposable = new CompositeDisposable();

		OnNavigatedTo(isInHistory, _currentDisposable);
	}

	protected virtual void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
	}

	private void DoNavigateFrom(bool isInHistory)
	{
		OnNavigatedFrom(isInHistory);

		_currentDisposable?.Dispose();
		_currentDisposable = null;
	}

	public void OnNavigatedTo(bool isInHistory)
	{
		DoNavigateTo(isInHistory);
	}

	void INavigatable.OnNavigatedFrom(bool isInHistory)
	{
		DoNavigateFrom(isInHistory);
	}

	protected virtual void OnNavigatedFrom(bool isInHistory)
	{
	}

	protected void EnableAutoBusyOn(params ICommand[] commands)
	{
		foreach (var command in commands)
		{
			(command as IReactiveCommand)?.IsExecuting
				.ObserveOn(RxApp.MainThreadScheduler)
				.Skip(1)
				.Subscribe(x => IsBusy = x);
		}
	}

	protected async Task ShowErrorAsync(string title, string message, string caption, NavigationTarget navigationTarget = NavigationTarget.Unspecified)
	{
		var target =
			navigationTarget != NavigationTarget.Unspecified
			? navigationTarget
			: CurrentTarget == NavigationTarget.CompactDialogScreen
				? NavigationTarget.CompactDialogScreen
				: NavigationTarget.DialogScreen;

		await UiContext.Navigate().Navigate(target).To().ShowErrorDialog(message, title, caption).GetResultAsync();
	}

	protected void SetupCancel(bool enableCancel, bool enableCancelOnEscape, bool enableCancelOnPressed, bool escapeGoesBack = false)
	{
		EnableCancel = enableCancel;
		EnableCancelOnEscape = enableCancelOnEscape && !escapeGoesBack;
		EnableCancelOnPressed = enableCancelOnPressed;
	}
}
