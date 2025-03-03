using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Views.Columns;

public partial class OrderLabelsColumnView : UserControl
{
	public OrderLabelsColumnView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
