using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WalletWasabi.Fluent.TransactionViewer.ViewModels;

namespace WalletWasabi.Fluent.TransactionViewer.Controls;

public class TransactionFlowLines : Control
{
	public const double RowHeight = 176;
	public const double MiddleWidth = 240;
	public static readonly StyledProperty<IReadOnlyList<TransactionViewerRow>?> RowsProperty =
		AvaloniaProperty.Register<TransactionFlowLines, IReadOnlyList<TransactionViewerRow>?>(nameof(Rows));
	public static readonly StyledProperty<double> ScrollOffsetProperty = AvaloniaProperty.Register<TransactionFlowLines, double>(nameof(ScrollOffset));
	public static readonly StyledProperty<TransactionViewerNode?> SelectedNodeProperty =
		AvaloniaProperty.Register<TransactionFlowLines, TransactionViewerNode?>(nameof(SelectedNode));
	public static readonly StyledProperty<IBrush?> OwnBrushProperty = AvaloniaProperty.Register<TransactionFlowLines, IBrush?>(nameof(OwnBrush));

	static TransactionFlowLines() => AffectsRender<TransactionFlowLines>(RowsProperty, ScrollOffsetProperty, SelectedNodeProperty, OwnBrushProperty);
	public IReadOnlyList<TransactionViewerRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
	public double ScrollOffset { get => GetValue(ScrollOffsetProperty); set => SetValue(ScrollOffsetProperty, value); }
	public TransactionViewerNode? SelectedNode { get => GetValue(SelectedNodeProperty); set => SetValue(SelectedNodeProperty, value); }
	public IBrush? OwnBrush { get => GetValue(OwnBrushProperty); set => SetValue(OwnBrushProperty, value); }

	public override void Render(DrawingContext context)
	{
		base.Render(context);
		if (Rows is not { } rows)
		{
			return;
		}
		var center = Bounds.Width / 2;
		var nodeY = Bounds.Height / 2 - 35;
		var first = Math.Max(0, (int)(ScrollOffset / RowHeight));
		var last = Math.Min(rows.Count, (int)Math.Ceiling((ScrollOffset + Bounds.Height) / RowHeight));
		using (context.PushClip(new Rect(Bounds.Size)))
		{
			for (var i = first; i < last; i++)
			{
				var y = i * RowHeight + RowHeight / 2 - ScrollOffset;
				if (rows[i].Input is { } input)
				{
					Draw(context, input, new Point(center - MiddleWidth / 2 - 12, y), new Point(center - 64, nodeY));
				}
				if (rows[i].Output is { } output)
				{
					Draw(context, output, new Point(center + 64, nodeY), new Point(center + MiddleWidth / 2 + 12, y));
				}
			}
			context.DrawLine(new Pen(OwnBrush, 1), new Point(center, nodeY + 42), new Point(center, nodeY + 100));
		}
	}

	private void Draw(DrawingContext context, TransactionViewerNode node, Point start, Point end)
	{
		var geometry = new StreamGeometry();
		using (var path = geometry.Open())
		{
			path.BeginFigure(start, false);
			var bend = (start.X + end.X) / 2;
			path.CubicBezierTo(new Point(bend, start.Y), new Point(bend, end.Y), end);
			path.EndFigure(false);
		}
		var brush = node.IsOwn ? OwnBrush : Brushes.SteelBlue;
		context.DrawGeometry(null, new Pen(brush, node == SelectedNode ? 4 : 2), geometry);
	}
}
