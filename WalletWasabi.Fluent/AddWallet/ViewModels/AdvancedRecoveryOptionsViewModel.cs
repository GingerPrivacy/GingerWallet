using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AdvancedRecoveryOptionsViewModel : DialogViewModelBase<int?>
{
	[AutoNotify] private string _minGapLimit;

	public AdvancedRecoveryOptionsViewModel(int minGapLimit)
	{
		Title = Resources.AdvancedRecoveryOptionsViewModelTitle;

		_minGapLimit = minGapLimit.ToString(Resources.Culture);

		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() => Close(result: int.Parse(MinGapLimit, Resources.Culture)),
			this.WhenAnyValue(x => x.MinGapLimit).Select(_ => !Validations.Any));
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, out var minGapLimit) ||
			minGapLimit is < KeyManager.AbsoluteMinGapLimit or > KeyManager.MaxGapLimit)
		{
			errors.Add(
				ErrorSeverity.Error,
				Resources.MustBeNumberBetween.SafeInject(KeyManager.AbsoluteMinGapLimit, KeyManager.MaxGapLimit));
		}
	}
}
