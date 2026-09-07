using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace PCBetaMAUI.Converters;

public class ContentTypeToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isReplyMode = value is bool replyMode && replyMode;
        var contentType = parameter as string ?? string.Empty;
        var isSelected = string.Equals(contentType, "reply", StringComparison.OrdinalIgnoreCase)
            ? isReplyMode
            : !isReplyMode;

        return isSelected ? Colors.LightSkyBlue : Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
