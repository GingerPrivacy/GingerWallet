using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.AddWallet.Views.HardwareWallet;

public class DetectedHardwareWalletView : UserControl
{
	public DetectedHardwareWalletView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
