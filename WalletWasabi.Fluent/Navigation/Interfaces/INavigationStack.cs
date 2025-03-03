using System.Threading.Tasks;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Navigation.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Navigation.Interfaces;

public interface INavigationStack<T> where T : INavigatable
{
	/// <summary>
	/// The Current Page.
	/// </summary>
	T? CurrentPage { get; }

	/// <summary>
	/// True if you can navigate back, else false.
	/// </summary>
	bool CanNavigateBack { get; }

	void To(T viewmodel, NavigationMode mode = NavigationMode.Normal);

	FluentNavigate To();

	void Back();

	void BackTo(T viewmodel);

	void BackTo<TViewModel>() where TViewModel : T;

	void Clear();

	Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationMode navigationMode = NavigationMode.Normal);
}
