using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.TransactionViewer.Controls;

namespace WalletWasabi.Fluent.TransactionViewer.Views;

public class TransactionFlowView : UserControl
{
	private ScrollViewer? _scrollViewer;
	private ScrollContentPresenter? _viewport;

	public TransactionFlowView() => AvaloniaXamlLoader.Load(this);

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);
		_scrollViewer = this.FindControl<ListBox>("CoinRows")!.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (_scrollViewer is { })
		{
			_scrollViewer.ScrollChanged += OnScrollChanged;
			_viewport = _scrollViewer.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
			if (_viewport is { })
			{
				_viewport.SizeChanged += OnViewportSizeChanged;
			}
			UpdateLines();
		}
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		if (_scrollViewer is { })
		{
			_scrollViewer.ScrollChanged -= OnScrollChanged;
		}
		if (_viewport is { })
		{
			_viewport.SizeChanged -= OnViewportSizeChanged;
		}
		base.OnUnloaded(e);
	}

	private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateLines();
	private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateLines();

	private void UpdateLines()
	{
		if (_scrollViewer is { } scroll)
		{
			var lines = this.FindControl<TransactionFlowLines>("FlowLines")!;
			lines.ScrollOffset = scroll.Offset.Y;
			if (_viewport is { Bounds.Width: > 0 } viewport)
			{
				this.FindControl<Grid>("FlowOverlay")!.Width = viewport.Bounds.Width;
			}
		}
	}
}
