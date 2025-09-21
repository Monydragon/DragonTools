using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DragonTools.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool notNull = value != null;
        return Invert ? !notNull : notNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
