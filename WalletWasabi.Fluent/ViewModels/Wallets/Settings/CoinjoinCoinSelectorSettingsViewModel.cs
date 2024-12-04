using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CoinjoinCoinSelectorSettingsViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private bool _forceUsingLowPrivacyCoins;
	[AutoNotify] private string _weightedAnonymityLossNormal;
	[AutoNotify] private string _valueLossRateNormal;
	[AutoNotify] private string _targetCoinCountPerBucket;

	public CoinjoinCoinSelectorSettingsViewModel(UiContext uiContext, IWalletModel wallet)
	{
		_wallet = wallet;
		UiContext = uiContext;
		Title = Resources.Configuration;

		NextCommand = ReactiveCommand.Create(() => Close());

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.WeightedAnonymityLossNormal, x => ValidateDouble(x, WeightedAnonymityLossNormal, 0.5, 5));
		this.ValidateProperty(x => x.ValueLossRateNormal, x => ValidateDouble(x, ValueLossRateNormal, 0.001, 0.05));
		this.ValidateProperty(x => x.TargetCoinCountPerBucket, x => ValidateDouble(x, TargetCoinCountPerBucket, 1.0, 30.0));

		_forceUsingLowPrivacyCoins = _wallet.Settings.ForceUsingLowPrivacyCoins;
		_weightedAnonymityLossNormal = _wallet.Settings.WeightedAnonymityLossNormal.ToString(CultureInfo.InvariantCulture);
		_valueLossRateNormal = _wallet.Settings.ValueLossRateNormal.ToString(CultureInfo.InvariantCulture);
		_targetCoinCountPerBucket = _wallet.Settings.TargetCoinCountPerBucket.ToString(CultureInfo.InvariantCulture);

		this.WhenAnyValue(x => x.ForceUsingLowPrivacyCoins)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				_wallet.Settings.ForceUsingLowPrivacyCoins = x;
				_wallet.Settings.Save();
			});

		this.WhenAnyValue(x => x.WeightedAnonymityLossNormal)
			.Skip(1)
			.Where(_ => !HasError(nameof(WeightedAnonymityLossNormal)))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (double.TryParse(x, out var result) && result != _wallet.Settings.WeightedAnonymityLossNormal)
				{
					_wallet.Settings.WeightedAnonymityLossNormal = result;
					_wallet.Settings.Save();
				}
			});

		this.WhenAnyValue(x => x.ValueLossRateNormal)
			.Skip(1)
			.Where(_ => !HasError(nameof(ValueLossRateNormal)))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (double.TryParse(x, out var result) && result != _wallet.Settings.ValueLossRateNormal)
				{
					_wallet.Settings.ValueLossRateNormal = result;
					_wallet.Settings.Save();
				}
			});

		this.WhenAnyValue(x => x.TargetCoinCountPerBucket)
			.Skip(1)
			.Where(_ => !HasError(nameof(TargetCoinCountPerBucket)))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (double.TryParse(x, out var result) && result != _wallet.Settings.TargetCoinCountPerBucket)
				{
					_wallet.Settings.TargetCoinCountPerBucket = result;
					_wallet.Settings.Save();
				}
			});
	}

	private void ValidateDouble(IValidationErrors errors, string value, double min, double max)
	{
		if (!double.TryParse(value, out var result))
		{
			errors.Add(ErrorSeverity.Error, Resources.ValidationErrorNotNumber);
			return;
		}
		if (result < min || result > max)
		{
			errors.Add(ErrorSeverity.Error, error: string.Format(CultureInfo.InvariantCulture, Resources.ValidationErrorNotInRange, min, max));
			return;
		}
	}
}
