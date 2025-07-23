using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.Converters;

public static class DefaultSourceConverters
{
	public static readonly IValueConverter NotNone =
		new FuncValueConverter<DefaultCommandSource, bool>(x => x != DefaultCommandSource.None);
}
