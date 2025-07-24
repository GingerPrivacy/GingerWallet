using System.Collections.Generic;

namespace WalletWasabi.Tor.StatusChecker.ApiModels;

public record SystemStatus(string Category, List<TorIssue> UnresolvedIssues);
