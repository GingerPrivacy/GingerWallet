using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.OpenDirectory.ViewModels;

public abstract class OpenFileViewModel : TriggerCommandViewModel
{
	public abstract string FilePath { get; }

	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				await UiContext.FileSystem.OpenFileInTextEditorAsync(FilePath);
			}
			catch (Exception ex)
			{
				await ShowErrorAsync(Resources.Open, ex.ToUserFriendlyString(), Resources.GingerWalletUnableToOpenFile);
			}
		});
}
