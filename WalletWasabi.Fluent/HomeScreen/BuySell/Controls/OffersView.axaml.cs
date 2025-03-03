using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Controls;

public class OffersView : UserControl
{
	public OffersView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
