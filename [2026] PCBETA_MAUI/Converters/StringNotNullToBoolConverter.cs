using System.Globalization;

namespace PCBetaMAUI.Converters;

/// <summary>
/// Converter that converts non-null and non-empty string to true
/// Used for conditional visibility of optional metadata (edit status, moderation info, etc.)
/// </summary>
public class StringNotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return !string.IsNullOrEmpty(str);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
