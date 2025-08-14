using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.Open,
	IconName = "folder_regular",
	IsLocalized = true)]
public partial class OpenWalletsFolderViewModel : TriggerCommandViewModel
{
	public OpenWalletsFolderViewModel()
	{
		TargetCommand = ReactiveCommand.Create(() => UiContext.FileSystem.OpenFolderInFileExplorer(UiContext.Config.WalletsDir));
	}

	public override ICommand TargetCommand { get; }
}
