using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Converters;

public sealed class PriorityToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoPriority p) return string.Empty;
        return p switch
        {
            TodoPriority.High => "!",   // high attention
            TodoPriority.Medium => "·", // middle dot
            TodoPriority.Low => "–",    // dash / low
            _ => string.Empty
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

