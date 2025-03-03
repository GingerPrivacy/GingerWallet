using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class SearchBarWidthBehavior : AttachedToVisualTreeBehavior<SearchBar.Views.SearchBar>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is not { } searchBar)
		{
			return;
		}

		if (searchBar.FindLogicalAncestorOfType<StackPanel>() is not { } container)
		{
			return;
		};

		Observable
			.FromEventPattern<EventHandler<AvaloniaPropertyChangedEventArgs>, AvaloniaPropertyChangedEventArgs>(
				h => container.PropertyChanged += h,
				h => container.PropertyChanged -= h)
			.Where(e => e.EventArgs.Property == Visual.BoundsProperty)
			.Select(_ => container.Bounds.Width)
			.DistinctUntilChanged()
			.Subscribe(totalWidth =>
			{
				double searchBarWidth = searchBar.Bounds.Width;
				totalWidth += searchBarWidth;
				double usedWidth = 0;
				double spacing = container.Spacing;

				for (int i = 0; i < container.Children.Count; i++)
				{
					var child = container.Children[i];
					usedWidth += child.Bounds.Width;

					if (i < container.Children.Count - 1)
					{
						usedWidth += spacing;
					}
				}

				double freeSpace = totalWidth - usedWidth - spacing;

				if (searchBar.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault() is { } textBox)
				{
					textBox.Width = freeSpace;
				}
			})
			.DisposeWith(disposable);
	}
}
