using WalletWasabi.Models;

namespace WalletWasabi.Daemon.FeeRateProviders;

public enum FeeRateProviderSource
{
	[FriendlyName("Mempool Space")]
	MempoolSpace,

	[FriendlyName("Blockstream Info")]
	BlockstreamInfo,

	[FriendlyName("Full Node")]
	FullNode
}
