using System.Collections.Generic;

namespace WalletWasabi.Tor.StatusChecker.ApiModels;

public record TorNetworkStatus(List<SystemStatus> Systems);
