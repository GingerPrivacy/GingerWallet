using System.Threading.Tasks;
using WalletWasabi.Fluent.Authorization.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IHardwareWalletModel : IWalletModel
{
	Task<bool> AuthorizeTransactionAsync(TransactionAuthorizationInfo transactionAuthorizationInfo);
}
