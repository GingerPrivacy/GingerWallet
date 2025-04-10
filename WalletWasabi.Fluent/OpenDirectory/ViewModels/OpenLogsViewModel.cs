using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.Open,
	IconName = "document_regular",
	IsLocalized = true)]
public partial class OpenLogsViewModel : OpenFileViewModel
{
	public override string FilePath => UiContext.Config.LoggerFilePath;
}
