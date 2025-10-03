using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DragonTools.Converters;

public sealed class DepthToIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int depth && depth >= 1)
        {
            // Depth 1 => 0, depth 2 => 16, depth 3 => 32
            var left = (depth - 1) * 16;
            return new Thickness(left, 0, 0, 0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

