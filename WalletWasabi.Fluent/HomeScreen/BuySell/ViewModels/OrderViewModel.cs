using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public class OrderViewModel : ViewModelBase
{
	public OrderViewModel(GetOrderModel model, BuySellModel buyModel)
	{
		Model = model;
		Labels = new LabelsArray([Model.ProviderName]);

		NavigateCommand = ReactiveCommand.CreateFromTask(async () => await OnOpenInBrowserAsync(Model.RedirectUrl));
		DetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().OrderDetails(model, buyModel));
	}

	public GetOrderModel Model { get; }

	public string DateString => Model.CreatedAt.ToUserFacingFriendlyString();
	public string DateToolTipString => Model.CreatedAt.ToUserFacingString();
	public LabelsArray Labels { get; }

	public ICommand NavigateCommand { get; }
	public ICommand DetailsCommand { get; }

	private async Task OnOpenInBrowserAsync(string url)
	{
		try
		{
			await WebBrowserService.Instance.OpenUrlInPreferredBrowserAsync(url).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Resources.Browser, Resources.BrowserError);
		}
	}
}
