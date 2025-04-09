using System.Globalization;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CustomFeeRateDialogViewModel : DialogViewModelBase<FeeRate>
{
	private readonly TransactionInfo _transactionInfo;

	[AutoNotify] private string _customFee;

	public CustomFeeRateDialogViewModel(TransactionInfo transactionInfo)
	{
		Title = Resources.CustomFeeRate;

		_transactionInfo = transactionInfo;

		_customFee = transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte.ToString("0.##", Resources.Culture.NumberFormat)
			: "";

		EnableBack = false;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.CustomFee, ValidateCustomFee);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.CustomFee)
				.Select(_ =>
				{
					var noError = !Validations.Any;
					var somethingFilled = CustomFee is not null or "";

					return noError && somethingFilled;
				});

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
	}

	private void OnNext()
	{
		if (decimal.TryParse(CustomFee, NumberStyles.AllowDecimalPoint, Resources.Culture.NumberFormat, out var feeRate))
		{
			_transactionInfo.IsCustomFeeUsed = true;
			Close(DialogResultKind.Normal, new FeeRate(feeRate));
		}
		else
		{
			_transactionInfo.IsCustomFeeUsed = false;
			Close(DialogResultKind.Normal, FeeRate.Zero); // must return zero which indicates that it was cleared.
		}
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		var customFeeString = CustomFee;

		if (customFeeString is "")
		{
			return;
		}

		if (!decimal.TryParse(customFeeString, NumberStyles.AllowDecimalPoint, Resources.Culture.NumberFormat, out var value))
		{
			errors.Add(ErrorSeverity.Error, Resources.InvalidFee);
			return;
		}

		if (value < decimal.One)
		{
			errors.Add(ErrorSeverity.Error, Resources.MinFeeLimit);
			return;
		}

		try
		{
			_ = new FeeRate(value);
		}
		catch (OverflowException)
		{
			errors.Add(ErrorSeverity.Error, Resources.FeeTooHigh);
			return;
		}
	}
}
