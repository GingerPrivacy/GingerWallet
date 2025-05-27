using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.Views;

public class ConfirmCoinjoinSettingsView : UserControl
{
	public ConfirmCoinjoinSettingsView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
