using System.Globalization;

namespace PCBetaMAUI.Converters;

/// <summary>
/// Converter that shows "−" (minus) when expanded, "+" (plus) when collapsed
/// </summary>
public class ExpandedToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "−" : "+";  // − is minus sign, + is plus sign
        }
        return "+";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
