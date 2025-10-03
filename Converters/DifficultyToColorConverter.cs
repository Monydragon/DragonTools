using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Converters;

public sealed class DifficultyToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoDifficulty diff) return Colors.Gray;
        // Tailwind-ish palette mapping
        return diff switch
        {
            TodoDifficulty.VeryEasy => Color.FromArgb("#6EE7B7"), // emerald 300
            TodoDifficulty.Easy => Color.FromArgb("#34D399"),     // emerald 400
            TodoDifficulty.Medium => Color.FromArgb("#10B981"),   // emerald 500
            TodoDifficulty.Hard => Color.FromArgb("#F59E0B"),     // amber 500
            TodoDifficulty.VeryHard => Color.FromArgb("#EF4444"), // red 500
            _ => Colors.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

