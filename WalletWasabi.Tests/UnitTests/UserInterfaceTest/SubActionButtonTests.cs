using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Models.UI;
using Xunit;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

[Collection("Serial unit tests collection")]
public class SubActionButtonTests
{
	[AvaloniaFact]
	public async Task ButtonLoadsWithoutTheMainApplicationContext()
	{
		var previousContext = UiContext.Default;
		Window? window = null;
		try
		{
			// The crash reporter runs without the wallet application's UiContext.
			UiContext.Default = null!;
			using var command = ReactiveCommand.Create(() => { });
			var first = new UICommand { Key = "first", Name = "First", Command = command };
			var button = new SubActionButton
			{
				Content = "Continue",
				SubCommands = new UICommandCollection { first }
			};
			window = new Window { Content = button, Width = 300, Height = 150 };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			Assert.True(button.IsLoaded);
			Assert.True(first.IsDefault);
			Assert.Same(command, button.GetValue(SubActionButton.CommandProperty));
			await ((ReactiveCommand<string, Unit>)button.GetValue(SubActionButton.SetDefaultCommandProperty)).Execute("first");
			Dispatcher.UIThread.RunJobs();
			Assert.Null(UiContext.Default);
		}
		finally
		{
			window?.Close();
			UiContext.Default = previousContext;
		}
	}
}
