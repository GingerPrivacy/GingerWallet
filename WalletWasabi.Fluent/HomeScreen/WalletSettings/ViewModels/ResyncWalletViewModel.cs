using System.Globalization;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

public record ResyncWalletDialogResult(int StartingHeight, int MinGapLimit);

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ResyncWalletViewModel : DialogViewModelBase<ResyncWalletDialogResult?>
{
	[AutoNotify] private string _startingHeight = "0";
	[AutoNotify] private string _minGapLimit;
	private readonly int _maxHeight;
	private readonly int _currentMinGapLimit;

	public ResyncWalletViewModel(int maxHeight, int minGapLimit)
	{
		Title = Resources.ResyncWallet;
		_maxHeight = maxHeight;
		_currentMinGapLimit = minGapLimit;
		_minGapLimit = minGapLimit.ToString(CultureInfo.InvariantCulture);
		this.ValidateProperty(x => x.StartingHeight, ValidateStartingHeight);
		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);
		SetupCancel(false, true, true);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal,
			new ResyncWalletDialogResult(int.Parse(StartingHeight, CultureInfo.InvariantCulture), int.Parse(MinGapLimit, CultureInfo.InvariantCulture))),
			this.WhenAnyValue(x => x.StartingHeight, x => x.MinGapLimit).Select(_ => !Validations.Any));
	}

	private void ValidateStartingHeight(IValidationErrors errors)
	{
		if (!int.TryParse(StartingHeight, NumberStyles.None, CultureInfo.InvariantCulture, out var height) || height < 0 || height > _maxHeight)
		{
			errors.Add(ErrorSeverity.Error, Resources.MustBeNumberBetween.SafeInject(0, _maxHeight));
		}
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, NumberStyles.None, CultureInfo.InvariantCulture, out var gap) || gap < _currentMinGapLimit || gap > KeyManager.MaxGapLimit)
		{
			errors.Add(ErrorSeverity.Error, Resources.MustBeNumberBetween.SafeInject(_currentMinGapLimit, KeyManager.MaxGapLimit));
		}
	}
}
