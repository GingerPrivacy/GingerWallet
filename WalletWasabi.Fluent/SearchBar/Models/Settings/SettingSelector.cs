using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace WalletWasabi.Fluent.SearchBar.Models.Settings;

[Localizable(false)]
public class SettingSelector : IDataTemplate
{
	public List<IDataTemplate> DataTemplates { get; set; } = new();

	public Control Build(object? param)
	{
		var prop = param?.GetType().GetProperty("Value");
		var template = DataTemplates.FirstOrDefault(d =>
		{
			var value = prop?.GetValue(param);

			if (value is null)
			{
				return false;
			}

			return d.Match(value);
		});

		if (template?.Build(param) is { } control)
		{
			return control;
		}

		return new TextBlock { Text = "Not found" };
	}

	public bool Match(object? data)
	{
		if (data is null)
		{
			return false;
		}

		return data.GetType().Name.Contains("Setting");
	}
}
