using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class ReceiveDefaultCommandTests
{
	[Fact]
	public void LocalPreferenceSelectsAndUpdatesTheReceiveCommand()
	{
		var segwit = new UICommand { Key = "SegWit", Command = ReactiveCommand.Create(() => { }) };
		var taproot = new UICommand { Key = "Taproot", Command = ReactiveCommand.Create(() => { }) };
		var button = new SubActionButton
		{
			DefaultSource = DefaultCommandSource.Receive,
			LocalDefaultKey = "Taproot",
			SubCommands = new UICommandCollection { segwit, taproot }
		};

		Assert.Same(taproot.Command, button.GetValue(SubActionButton.CommandProperty));
		Assert.True(taproot.IsDefault);
		Assert.False(segwit.IsDefault);

		button.SetLocalDefaultCommand = ReactiveCommand.Create<string>(key => button.LocalDefaultKey = key);
		button.GetValue(SubActionButton.SetDefaultCommandProperty).Execute("SegWit");
		Assert.Same(segwit.Command, button.GetValue(SubActionButton.CommandProperty));
		Assert.True(segwit.IsDefault);
		Assert.False(taproot.IsDefault);
	}
}
