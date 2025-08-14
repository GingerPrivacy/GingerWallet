using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.SignMessage.Views;

public class HardwareWalletMessageSigningView : UserControl
{
	public HardwareWalletMessageSigningView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
