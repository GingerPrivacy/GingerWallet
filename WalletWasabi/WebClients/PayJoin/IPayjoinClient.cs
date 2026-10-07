using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WebClients.PayJoin;

public interface IPayjoinClient
{
	Uri PaymentUrl { get; }

	Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey segwitAccountKey, RootedKeyPath segwitRootedKeyPath, IHDKey? taprootAccountKey, RootedKeyPath taprootRootedKeyPath, HdPubKey? changeHdPubKey, CancellationToken cancellationToken);
}
