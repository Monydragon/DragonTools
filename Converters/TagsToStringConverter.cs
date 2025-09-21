using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;

namespace DragonTools.Converters;

public sealed class TagsToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<string> tags)
        {
            var list = tags.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (list.Count == 0) return string.Empty;
            return $"Tags: {string.Join(", ", list)}";
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
