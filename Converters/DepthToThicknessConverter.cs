using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DragonTools.Converters;

public sealed class DepthToThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var depth = 0;
        if (value is int i) depth = i;
        else if (value is long l) depth = (int)l;
        if (depth < 1) depth = 1;
        var left = (depth - 1) * 16; // 16 px per depth level
        return new Thickness(left, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
