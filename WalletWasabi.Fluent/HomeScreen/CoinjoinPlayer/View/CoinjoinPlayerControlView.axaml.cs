using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.CoinjoinPlayer.View;

public class CoinjoinPlayerControlView : UserControl
{
	public CoinjoinPlayerControlView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
