namespace WalletWasabi.Fluent.Navigation.Interfaces;

public interface INavigatable
{
	void OnNavigatedTo(bool isInHistory);

	void OnNavigatedFrom(bool isInHistory);
}
