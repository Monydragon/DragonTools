using System;
using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DragonTools.Converters;

public sealed class TagOverflowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable list) return string.Empty;
        int max = 5;
        if (parameter != null && int.TryParse(parameter.ToString(), out var p)) max = p;
        int count = 0;
        foreach (var _ in list) count++;
        if (count > max) return $"+{count - max}";
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

