using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.Open,
	IconName = "folder_regular",
	IsLocalized = true)]
public partial class OpenDataFolderViewModel : TriggerCommandViewModel
{
	public OpenDataFolderViewModel()
	{
		TargetCommand = ReactiveCommand.Create(() => UiContext.FileSystem.OpenFolderInFileExplorer(UiContext.Config.DataDir));
	}

	public override ICommand TargetCommand { get; }
}
