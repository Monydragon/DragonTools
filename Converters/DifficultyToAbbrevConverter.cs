using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Converters;

public sealed class DifficultyToAbbrevConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoDifficulty d) return string.Empty;
        return d switch
        {
            TodoDifficulty.VeryEasy => "VE",
            TodoDifficulty.Easy => "E",
            TodoDifficulty.Medium => "M",
            TodoDifficulty.Hard => "H",
            TodoDifficulty.VeryHard => "VH",
            _ => string.Empty
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

