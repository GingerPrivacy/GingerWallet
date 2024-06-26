using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "About Ginger Wallet",
	Caption = "Display Ginger Wallet's current info",
	IconName = "info_regular",
	Order = 4,
	Category = "Help & Support",
	Keywords = new[]
	{
			"About", "Software", "Version", "Source", "Code", "Github", "Website", "Coordinator", "Stats", "Tor", "Onion",
			"User", "Support", "Bug", "Report", "FAQ", "Questions,", "Docs", "Documentation", "License", "Advanced", "Information",
			"Hardware", "Wallet"
	},
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
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
					Link = DocsLink,
					Description = "Documentation",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = SourceCodeLink,
					Description = "Source Code (GitHub)",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = ClearnetLink,
					Description = "Website (Clearnet)",
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
					Description = "User Support",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = BugReportLink,
					Description = "Bug Report",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = FAQLink,
					Description = "FAQ",
					IsClickable = true
				},
			};

		License = new LinkViewModel(UiContext)
		{
			Link = LicenseLink,
			Description = "MIT License",
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

	public static string ClearnetLink => "https://gingerwallet.io/";

	public static string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

	public static string SourceCodeLink => "https://github.com/GingerPrivacy/GingerWallet";

	public static string UserSupportLink => "https://github.com/GingerPrivacy/GingerWallet/discussions";

	public static string BugReportLink => "https://github.com/GingerPrivacy/GingerWallet/issues/new?template=bug-report.md";

	public static string FAQLink => "https://gingerwallet.io/#help";

	public static string DocsLink => "https://gingerwallet.io/#why";

	public static string LicenseLink => "https://github.com/GingerPrivacy/GingerWallet/blob/master/LICENSE.md";
}
