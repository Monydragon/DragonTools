using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Converters;

/// <summary>
/// Converts a TodoDifficulty enum to a user-friendly full name.
/// </summary>
public sealed class DifficultyToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TodoDifficulty d)
        {
            return d switch
            {
                TodoDifficulty.VeryEasy => "Trivial", // map to wording in mock
                TodoDifficulty.Easy => "Easy",
                TodoDifficulty.Medium => "Normal",
                TodoDifficulty.Hard => "Hard",
                TodoDifficulty.VeryHard => "Very Hard",
                _ => d.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

