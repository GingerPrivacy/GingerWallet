using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Helpers;

public static class ConfirmationTimeLabel
{
	public static string AxisLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return Resources.Fastest;
		}

		return TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration(Resources.day, Resources.hour, Resources.min));
	}

	public static string SliderLabel(TimeSpan timeSpan)
	{
		if (timeSpan <= TransactionFeeHelper.CalculateConfirmationTime(WalletWasabi.Helpers.Constants.FastestConfirmationTarget))
		{
			return Resources.Fastest;
		}

		return "~" + TimeSpanFormatter.Format(timeSpan, new TimeSpanFormatter.Configuration($" {Resources.day}", $" {Resources.hour}", $" {Resources.min}"));
	}
}
