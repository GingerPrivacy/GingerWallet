namespace WalletWasabi.Daemon;

public record DefaultCommands(
	string? SendDefaultKey = null,
	string? ReceiveDefaultKey = null);
