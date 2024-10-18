using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters;

public static class StatusConverters
{
	public static readonly IValueConverter TorStatusToString =
		new FuncValueConverter<TorStatus, string>(x => x switch
		{
			TorStatus.Running => Resources.IsRunning,
			TorStatus.NotRunning => Resources.IsNotRunning,
			TorStatus.TurnedOff => Resources.IsTurnedOff,
			{ } => x.ToString()
		});

	public static readonly IValueConverter BackendStatusToString =
		new FuncValueConverter<BackendStatus, string>(x => x switch
		{
			BackendStatus.Connected => Resources.IsConnected,
			BackendStatus.NotConnected => Resources.IsNotConnected,
			{ } => x.ToString()
		});

	public static readonly IValueConverter RpcStatusStringConverter =
		new FuncValueConverter<RpcStatus?, string>(status => status is null ? RpcStatus.Unresponsive.ToString() : status.ToString());

	public static readonly IValueConverter PeerStatusStringConverter =
		new FuncValueConverter<int, string>(peerCount => string.Format(CultureInfo.InvariantCulture, Resources.PeersConnected, peerCount));
}
