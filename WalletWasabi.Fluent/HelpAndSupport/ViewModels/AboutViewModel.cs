using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(
	IconName = "info_regular",
	Order = 4,
	Category = SearchCategory.HelpAndSupport,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true)]
public partial class AboutViewModel : RoutableViewModel
{
	public AboutViewModel(UiContext uiContext, bool navigateBack = false)
	{
		UiContext = uiContext;

		EnableBack = navigateBack;

		Links = new List<ViewModelBase>()
			{
				new LinkViewModel(UiContext)
				{
					Link = SourceCodeLink,
					Description = Resources.SourceCodeGitHub,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = ClearnetLink,
					Description = Resources.WebsiteClearnet,
					IsClickable = true
				},
				// Remove for now
				/*new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = TorLink,
					Description = "Website (Tor)",
					IsClickable = false
				},*/
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = UserSupportLink,
					Description = Resources.UserSupportViewModelTitle,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = BugReportLink,
					Description = Resources.BugReport,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = FAQLink,
					Description = Resources.FAQ,
					IsClickable = true
				},
			};

		License = new LinkViewModel(UiContext)
		{
			Link = LicenseLink,
			Description = Resources.MITLicense,
			IsClickable = true
		};

		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(x => UiContext.FileSystem.OpenBrowserAsync(x));

		AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(async () => await Navigate().To().AboutAdvancedInfo().GetResultAsync());

		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.Clipboard.SetTextAsync(link));

		NextCommand = CancelCommand;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public List<ViewModelBase> Links { get; }

	public LinkViewModel License { get; }

	public ICommand AboutAdvancedInfoDialogCommand { get; }

	public ICommand OpenBrowserCommand { get; }

	public ICommand CopyLinkCommand { get; }

	public Version ClientVersion => Constants.ClientVersion;

	[Localizable(false)]
	public static string ClearnetLink => "https://gingerwallet.io/";

	[Localizable(false)]
	public static string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

	[Localizable(false)]
	public static string SourceCodeLink => "https://github.com/GingerPrivacy/GingerWallet";

	[Localizable(false)]
	public static string UserSupportLink => "https://github.com/GingerPrivacy/GingerWallet/discussions";

	[Localizable(false)]

	public static string BugReportLink => "https://github.com/GingerPrivacy/GingerWallet/issues/new?template=bug-report.md";

	[Localizable(false)]
	public static string FAQLink => "https://gingerwallet.io/#help";

	[Localizable(false)]
	public static string LicenseLink => "https://github.com/GingerPrivacy/GingerWallet/blob/master/LICENSE.md";
}
