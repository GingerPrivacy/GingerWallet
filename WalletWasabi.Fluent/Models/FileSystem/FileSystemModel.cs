using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Models.FileSystem;

public class FileSystemModel
{
	public Task OpenFileInTextEditorAsync(string filePath)
	{
		return FileHelpers.OpenFileInTextEditorAsync(filePath);
	}

	public void OpenFolderInFileExplorer(string dirPath)
	{
		IoHelpers.OpenFolderInFileExplorer(dirPath);
	}

	public Task OpenBrowserAsync(string url)
	{
		return WebBrowserService.Instance.OpenUrlInPreferredBrowserAsync(url);
	}
}
