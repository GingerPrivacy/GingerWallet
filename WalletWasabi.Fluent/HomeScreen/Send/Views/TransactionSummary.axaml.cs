using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.Send.Views;

public class TransactionSummary : UserControl
{
	public TransactionSummary()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
