using System;
using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Converters;

public class SelectedFilterToBackgroundConverter : IValueConverter
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
                // selected color - adapt to light/dark modes if needed
                return Colors.LightSkyBlue;
            }

            // default transparent so button looks normal
            return Colors.Transparent;
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
