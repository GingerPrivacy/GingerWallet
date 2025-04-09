using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.Open,
	IconName = "document_regular",
	IsLocalized = true)]
public partial class OpenTorLogsViewModel : OpenFileViewModel
{
	public override string FilePath => UiContext.Config.TorLogFilePath;
}
