using System;
using Microsoft.Maui.Storage;

namespace DragonTools.Services;

public enum AppThemePref { System = 0, Light = 1, Dark = 2 }
public enum BulkPlaceholderStyle { SingleLine = 0, MultiLine = 1 }

public sealed class RandomPickerSettings
{
    public bool Deduplicate { get; set; } = true;
    public bool NormalizeSpaces { get; set; } = true;
    public BulkPlaceholderStyle PlaceholderStyle { get; set; } = BulkPlaceholderStyle.SingleLine;
}

public static class SettingsService
{
    // --------- Keys
    const string ThemeKey = "app.theme";
    const string RP_DedupKey = "rp.dedup";
    const string RP_NormKey = "rp.norm";
    const string RP_PlaceholderKey = "rp.placeholder";

    // --------- Global theme
    public static AppThemePref GetTheme()
    {
        var v = Preferences.Get(ThemeKey, (int)AppThemePref.System);
        return Enum.IsDefined(typeof(AppThemePref), v) ? (AppThemePref)v : AppThemePref.System;
    }

    public static void SetTheme(AppThemePref pref)
    {
        Preferences.Set(ThemeKey, (int)pref);
        var actual = pref switch
        {
            AppThemePref.Light => AppTheme.Light,
            AppThemePref.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
        Application.Current!.UserAppTheme = actual;
    }

    // --------- Random Picker
    public static RandomPickerSettings GetRandomPicker()
    {
        return new RandomPickerSettings
        {
            Deduplicate = Preferences.Get(RP_DedupKey, true),
            NormalizeSpaces = Preferences.Get(RP_NormKey, true),
            PlaceholderStyle = (BulkPlaceholderStyle)Preferences.Get(RP_PlaceholderKey, (int)BulkPlaceholderStyle.SingleLine),
        };
    }

    public static void SetRandomPicker(RandomPickerSettings s)
    {
        Preferences.Set(RP_DedupKey, s.Deduplicate);
        Preferences.Set(RP_NormKey, s.NormalizeSpaces);
        Preferences.Set(RP_PlaceholderKey, (int)s.PlaceholderStyle);
        RandomPickerChanged?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? RandomPickerChanged;
}
