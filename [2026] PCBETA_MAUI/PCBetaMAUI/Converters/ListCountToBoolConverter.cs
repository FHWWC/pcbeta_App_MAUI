using System.Collections;
using System.Collections.Specialized;
using System.Globalization;

namespace PCBetaMAUI.Converters;

/// <summary>
/// Converter that converts non-empty collection to true
/// Used for checking if a list has items to show/hide UI elements
/// Supports IList, ICollection, and INotifyCollectionChanged (ObservableCollection)
/// </summary>
public class ListCountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        // Support for ICollection (most common, includes ObservableCollection)
        if (value is ICollection collection)
        {
            return collection.Count > 0;
        }

        // Support for IList (arrays, lists)
        if (value is IList list)
        {
            return list.Count > 0;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
