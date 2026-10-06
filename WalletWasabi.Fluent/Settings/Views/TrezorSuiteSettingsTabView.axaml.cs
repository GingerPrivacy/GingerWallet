using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Settings.Views;

public class TrezorSuiteSettingsTabView : UserControl
{
	public TrezorSuiteSettingsTabView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
