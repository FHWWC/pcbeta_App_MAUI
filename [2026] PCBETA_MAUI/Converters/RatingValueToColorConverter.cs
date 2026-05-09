using System.Globalization;

namespace PCBetaMAUI.Converters;

/// <summary>
/// Converts rating value to button background color
/// If the parameter matches the binding value, returns selected color, otherwise returns unselected color
/// </summary>
public class RatingValueToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Colors.Gray;

        if (int.TryParse(value.ToString(), out var currentScore) && 
            int.TryParse(parameter.ToString(), out var buttonScore))
        {
            // If this button's score matches the current selected score, highlight it
            return currentScore == buttonScore ? Colors.OrangeRed : Colors.DarkGray;
        }

        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
