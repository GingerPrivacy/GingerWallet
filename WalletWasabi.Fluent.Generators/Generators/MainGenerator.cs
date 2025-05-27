using Microsoft.CodeAnalysis;
using WalletWasabi.Fluent.Generators.Abstractions;

namespace WalletWasabi.Fluent.Generators.Generators;

[Generator]
internal class MainGenerator : CombinedGenerator
{
	public MainGenerator()
	{
		AddStaticFileGenerator<AutoNotifyAttributeGenerator>();
		Add<AutoNotifyGenerator>();
		Add<FluentNavigationGenerator>();
	}
}
