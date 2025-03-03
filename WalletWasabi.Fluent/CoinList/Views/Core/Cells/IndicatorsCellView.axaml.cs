using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.CoinList.Views.Core.Cells;

public class IndicatorsCellView : UserControl
{
	public IndicatorsCellView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
