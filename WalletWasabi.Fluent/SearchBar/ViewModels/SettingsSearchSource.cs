using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;
using WalletWasabi.Fluent.SearchBar.Models.Settings;
using WalletWasabi.Fluent.SearchBar.ViewModels.SearchItems;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.SearchBar.ViewModels;

public class SettingsSearchSource : ReactiveObject, ISearchSource
{
	private readonly UiContext _uiContext;
	private readonly IApplicationSettings _applicationSettings;

	public SettingsSearchSource(UiContext uiContext, IObservable<string> query)
	{
		_uiContext = uiContext;
		_applicationSettings = uiContext.ApplicationSettings;

		var filter = query.Select(SearchSource.DefaultFilter);

		Changes = GetSettingsItems()
			.ToObservable()
			.ToObservableChangeSet(x => x.Key)
			.Filter(filter);
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }

	private IEnumerable<ISearchItem> GetSettingsItems()
	{
		var isEnabled = !_applicationSettings.IsOverridden;

		yield return new ContentSearchItem(content: Setting(selector: x => x.DarkModeEnabled), name: Resources.DarkMode, category: Resources.Appearance, keywords: Resources.AppearanceThemeKeywords.ToKeywords(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 1 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.AutoCopy), name: Resources.AutoCopyAddresses, category: Resources.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 2 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.AutoPaste), name: Resources.AutoPasteAddresses, category: Resources.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 3 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.HideOnClose), name: Resources.RunInBackgroundWhenClosed, category: Resources.Settings, keywords: Resources.RunInBackgroundKeywords.ToKeywords(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 4 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.RunOnSystemStartup), name: Resources.RunAtStartup, category: Resources.Settings, keywords: Resources.RunAtStartupKeywords.ToKeywords(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 5 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.EnableGpu), name: Resources.EnableGPU, category: Resources.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 6 };
	}

	private Setting<ApplicationSettings, TProperty> Setting<TProperty>(Expression<Func<ApplicationSettings, TProperty>> selector)
	{
		return new Setting<ApplicationSettings, TProperty>((ApplicationSettings)_applicationSettings, selector);
	}
}
