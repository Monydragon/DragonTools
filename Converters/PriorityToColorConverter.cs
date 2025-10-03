using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Converters;

public sealed class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoPriority p) return Colors.Gray;
        return p switch
        {
            TodoPriority.Low => Color.FromArgb("#3B82F6"),    // blue 500
            TodoPriority.Medium => Color.FromArgb("#F59E0B"), // amber 500
            TodoPriority.High => Color.FromArgb("#EF4444"),   // red 500
            _ => Colors.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

