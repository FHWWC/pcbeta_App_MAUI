using System.Globalization;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Converters;

/// <summary>
/// ✅ 新增：字符串相等比较 Converter
/// 用于 XAML 中比较字符串值和参数是否相等
/// 用途：错误提示条件显示（当 SelectedTypeId == "0" 时显示）
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && parameter is string strParam)
        {
            return strValue.Equals(strParam);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
