using System.Globalization;

namespace PCBetaMAUI.Converters;

/// <summary>
/// Converter that returns true if value is not equal to "0" or 0
/// Supports string, int, and other numeric types
/// </summary>
public class StringNotEqualZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        // Handle string values
        if (value is string str)
        {
            return str != "0" && !string.IsNullOrEmpty(str);
        }

        // Handle numeric values (int, long, float, double, etc.)
        if (value is int intValue)
        {
            return intValue != 0;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        if (value is double doubleValue)
        {
            return doubleValue != 0;
        }

        if (value is float floatValue)
        {
            return floatValue != 0;
        }

        // Try to parse as int if it's a different type
        if (int.TryParse(value.ToString(), out int parsedValue))
        {
            return parsedValue != 0;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
