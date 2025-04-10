using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class NumberFormatExampleConverter : AvaloniaObject, IValueConverter
{
	public enum ParameterType
	{
		DecimalSeparator,
		GroupSeparator
	}

	public static readonly StyledProperty<ParameterType> TypeProperty =
		AvaloniaProperty.Register<NumberFormatExampleConverter, ParameterType>(nameof(Type));

	public static readonly StyledProperty<decimal> ExampleNumberProperty =
		AvaloniaProperty.Register<NumberFormatExampleConverter, decimal>(nameof(ExampleNumber), 1000);

	public ParameterType Type
	{
		get => GetValue(TypeProperty);
		set => SetValue(TypeProperty, value);
	}

	public decimal ExampleNumber
	{
		get => GetValue(ExampleNumberProperty);
		set => SetValue(ExampleNumberProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null)
		{
			return AvaloniaProperty.UnsetValue;
		}

		var stringValue = value.ToString() ?? "";

		var decimalSeparator = ".";
		var groupSeparator = "";

		if (Type == ParameterType.DecimalSeparator)
		{
			decimalSeparator = stringValue;
		}
		else if (Type == ParameterType.GroupSeparator)
		{
			groupSeparator = stringValue;
		}

		CultureInfo customCulture = (CultureInfo)Lang.Resources.Culture.Clone();
		NumberFormatInfo formatInfo = new()
		{
			CurrencyGroupSeparator = groupSeparator,
			NumberGroupSeparator = groupSeparator,
			CurrencyDecimalSeparator = decimalSeparator,
			NumberDecimalSeparator = decimalSeparator
		};
		customCulture.NumberFormat = formatInfo;

		string number = ExampleNumber % 1 == 0
			? ExampleNumber.ToString("N0", customCulture)
			: ExampleNumber.ToString("G", customCulture);

		return number;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
