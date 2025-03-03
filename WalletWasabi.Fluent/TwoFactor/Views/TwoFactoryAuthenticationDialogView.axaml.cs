using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.TwoFactor.Views;

public class TwoFactoryAuthenticationDialogView : UserControl
{
	public TwoFactoryAuthenticationDialogView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
