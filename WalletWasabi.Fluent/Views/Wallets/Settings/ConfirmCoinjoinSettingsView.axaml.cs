using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.Settings;

public partial class ConfirmCoinjoinSettingsView : UserControl
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
