using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactions.Custom;
using DynamicData;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class TwoFactorTextBoxBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject
			.AddDisposableHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
			.DisposeWith(disposable);

		AssociatedObject
			.AddDisposableHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel)
			.DisposeWith(disposable);
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox tb)
		{
			return;
		}

		var prev = KeyboardNavigationHandler.GetNext(tb, NavigationDirection.Previous) as TextBox;
		var next = KeyboardNavigationHandler.GetNext(tb, NavigationDirection.Next) as TextBox;

		if (prev is { } && e.Key == Key.Left && tb.CaretIndex == 0 && GetIndex(prev) < GetIndex(tb))
		{
			prev.Focus();
			return;
		}

		if (next is { } && e.Key == Key.Right && tb.CaretIndex == 1 && GetIndex(next) > GetIndex(tb))
		{
			next.Focus();
			return;
		}

		if (prev is { } && e.Key == Key.Back && tb.CaretIndex == 0 && GetIndex(prev) < GetIndex(tb))
		{
			prev.ClearValue(TextBox.TextProperty);
			prev.Focus();
			return;
		}

		var keymap = Application.Current?.PlatformSettings?.HotkeyConfiguration;
		bool Match(IEnumerable<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));
		if (keymap is { } && Match(keymap.Paste))
		{
			e.Handled = true;
			ModifiedPasteAsync();
			return;
		}
	}

	private async void ModifiedPasteAsync()
	{
		var text = await ApplicationHelper.GetTextAsync();

		if (string.IsNullOrEmpty(text) || AssociatedObject is null)
		{
			return;
		}

		if (text.Length != 8 || text.Any(c => !char.IsDigit(c)))
		{
			return;
		}

		var container = AssociatedObject.Parent?.Parent;
		if (container is null)
		{
			return;
		}

		var items = container.GetLogicalChildren().Select(x => (x as ContentPresenter)?.Child as TextBox).ToArray();
		for (var i = 0; i < items.Length; i++)
		{
			var textBox = items[i];
			if (textBox is not { })
			{
				return;
			}

			textBox.SetValue(TextBox.TextProperty, $"{text[i]}");
		}
	}

	private void OnTextInput(object? sender, TextInputEventArgs e)
	{
		if (e.Text != null && e.Text.Any(x => !char.IsDigit(x)))
		{
			e.Handled = true;
			return;
		}

		if (sender is TextBox tb && KeyboardNavigationHandler.GetNext(tb, NavigationDirection.Next) is TextBox nextFocus && GetIndex(nextFocus) > GetIndex(tb))
		{
			nextFocus.Focus();
		}
	}

	private int GetIndex(TextBox tb)
	{
		var container = tb.Parent?.Parent;

		if (container is null)
		{
			return -1;
		}

		var items = container.GetLogicalChildren().Select(x => (x as ContentPresenter)?.Child as TextBox).ToArray();
		return items.IndexOf(tb);
	}
}
