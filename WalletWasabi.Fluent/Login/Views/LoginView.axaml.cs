using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Login.Views;

public class LoginView : UserControl
{
	public LoginView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
