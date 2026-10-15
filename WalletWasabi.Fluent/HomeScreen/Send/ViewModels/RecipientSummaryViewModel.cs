using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

public record RecipientSummaryViewModel(int Number, string Address, LabelsArray Labels, Amount Amount);
