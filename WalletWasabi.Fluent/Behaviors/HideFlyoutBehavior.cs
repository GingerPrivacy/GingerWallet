using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class HideFlyoutBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<bool> HideProperty =
		AvaloniaProperty.Register<ExecuteCommandOnActivatedBehavior, bool>(nameof(Hide));

	public bool Hide
	{
		get => GetValue(HideProperty);
		set => SetValue(HideProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null || FlyoutBase.GetAttachedFlyout(AssociatedObject) is not Flyout flyout)
		{
			return;
		}

		this.WhenAnyValue(x => x.Hide)
			.Where(x => x)
			.Do(_ => flyout.Hide())
			.Subscribe()
			.DisposeWith(disposable);
	}
}
