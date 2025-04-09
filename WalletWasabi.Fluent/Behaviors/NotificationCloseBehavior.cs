using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class NotificationCloseBehavior : AttachedToVisualTreeBehavior<Button>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { } button)
		{
			button.Click += OnCloseButtonClick;
			disposable.Add(Disposable.Create(() => button.Click -= OnCloseButtonClick));
		}
	}

	private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
	{
		if (sender is Button button)
		{
			var notificationCard = button.GetLogicalAncestors()
				.OfType<NotificationCard>()
				.FirstOrDefault();

			notificationCard?.Close();
		}
	}
}
