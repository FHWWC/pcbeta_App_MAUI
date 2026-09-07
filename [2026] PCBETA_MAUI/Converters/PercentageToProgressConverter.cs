using System.Globalization;

namespace PCBetaMAUI.Converters;

public class PercentageToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percentage)
            return Math.Clamp(percentage / 100d, 0d, 1d);

        if (value is IConvertible convertible)
        {
            try
            {
                var numericPercentage = convertible.ToDouble(CultureInfo.InvariantCulture);
                return Math.Clamp(numericPercentage / 100d, 0d, 1d);
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
        }

        return 0d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
