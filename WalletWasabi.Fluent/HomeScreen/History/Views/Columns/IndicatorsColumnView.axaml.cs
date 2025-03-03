using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.History.Views.Columns;

public class IndicatorsColumnView : UserControl
{
	public IndicatorsColumnView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
