using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

[NavigationMetaData(
	Order = 4,
	Category = SearchCategory.Open,
	IconName = "document_regular",
	IsLocalized = true)]
public partial class OpenConfigFileViewModel : OpenFileViewModel
{
	public override string FilePath => UiContext.Config.ConfigFilePath;
}
