using System.ComponentModel;

namespace WalletWasabi.Models;

public enum DisplayLanguage
{
	[Description("en-US")]
	English = 1,

	[Description("es-ES")]
	Spanish = 2,

	[Description("hu-HU")]
	Hungarian = 3,

	[Description("fr-FR")]
	French = 4,

	[Description("zh-SG")]
	Chinese = 5,

	[Description("de-DE")]
	German = 6,

	[Description("pt-PT")]
	Portuguese = 7,

	[Description("tr-TR")]
	Turkish = 8,

	[Description("it-IT")]
	Italian = 9
}
