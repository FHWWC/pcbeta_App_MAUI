using System;
using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Converters;

public class SelectedFilterToTextColorConverter : IValueConverter
{
    // value: SelectedFilter (string)
    // parameter: this button's filter key (string)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var selected = value as string ?? string.Empty;
            var key = parameter as string ?? string.Empty;

            if (string.Equals(selected, key, StringComparison.OrdinalIgnoreCase))
            {
                return Colors.White;
            }

            // default text color - use black here for safety
            return Colors.Black;
        }
        catch
        {
            return Colors.Black;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
