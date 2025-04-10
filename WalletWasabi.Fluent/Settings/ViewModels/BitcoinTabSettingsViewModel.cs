using System.Collections.Generic;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.Settings,
	IconName = "settings_bitcoin_regular",
	IsLocalized = true)]
public partial class BitcoinTabSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private string _bitcoinP2PEndPoint;
	[AutoNotify] private string _dustThreshold;

	public BitcoinTabSettingsViewModel(IApplicationSettings settings)
	{
		Settings = settings;

		this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);

		_bitcoinP2PEndPoint = settings.BitcoinP2PEndPoint;
		_dustThreshold = settings.DustThreshold.ToString(Resources.Culture.NumberFormat);

		this.WhenAnyValue(x => x.Settings.BitcoinP2PEndPoint)
			.Subscribe(x => BitcoinP2PEndPoint = x);

		this.WhenAnyValue(x => x.DustThreshold)
			.Skip(1)
			.Subscribe(x =>
			{
				x = x.PrepareForMoneyParsing();
				if (Money.TryParse(x, out var result))
				{
					Settings.DustThreshold = result;
				}
			});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public IEnumerable<Network> Networks { get; } = new[] { Network.Main, Network.RegTest };

	private void ValidateBitcoinP2PEndPoint(IValidationErrors errors)
	{
		if (!string.IsNullOrWhiteSpace(BitcoinP2PEndPoint))
		{
			if (!EndPointParser.TryParse(BitcoinP2PEndPoint, Settings.Network.DefaultPort, out _))
			{
				errors.Add(ErrorSeverity.Error, Resources.InvalidEndpoint);
			}
			else
			{
				Settings.BitcoinP2PEndPoint = BitcoinP2PEndPoint;
			}
		}
	}
}
