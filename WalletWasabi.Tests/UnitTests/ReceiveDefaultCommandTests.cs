using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class ReceiveDefaultCommandTests
{
	[Fact]
	public void LocalPreferenceSelectsAndUpdatesTheReceiveCommand()
	{
		var generated = 0;
		var saved = 0;
		var segwit = new UICommand { Key = "SegWit", Command = ReactiveCommand.Create(() => generated++) };
		var taproot = new UICommand { Key = "Taproot", Command = ReactiveCommand.Create(() => generated++) };
		var button = new SubActionButton
		{
			Content = "Generate",
			DefaultSource = DefaultCommandSource.Receive,
			LocalDefaultKey = "Taproot",
			SubCommands = new UICommandCollection { segwit, taproot }
		};

		Assert.Same(taproot.Command, button.GetValue(SubActionButton.CommandProperty));
		Assert.True(taproot.IsDefault);
		Assert.False(segwit.IsDefault);

		button.SetLocalDefaultCommand = ReactiveCommand.Create<string>(key =>
		{
			button.LocalDefaultKey = key;
			saved++;
		});
		button.GetValue(SubActionButton.SetDefaultCommandProperty).Execute("SegWit");
		Assert.Same(segwit.Command, button.GetValue(SubActionButton.CommandProperty));
		Assert.True(segwit.IsDefault);
		Assert.False(taproot.IsDefault);
		Assert.Equal(1, saved);
		Assert.Equal(0, generated);
		Assert.Equal("Generate", button.Content);

		button.GetValue(SubActionButton.CommandProperty).Execute(null);
		Assert.Equal(1, generated);
		Assert.Equal(1, saved);
	}
}
