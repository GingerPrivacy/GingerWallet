using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Dialogs;

public class VerifyTwoFactoryAuthenticationDialogView : UserControl
{
	public VerifyTwoFactoryAuthenticationDialogView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
