using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

public enum AppThemeOption { System, Light, Dark }

public static class ThemeManager
{
    private const string PrefKey = "AppThemeOption";

    public static AppThemeOption GetThemeOption()
    {
        var value = Preferences.Get(PrefKey, nameof(AppThemeOption.System));
        return Enum.TryParse<AppThemeOption>(value, out var opt) ? opt : AppThemeOption.System;
    }

    public static void SetTheme(AppThemeOption option)
    {
        Preferences.Set(PrefKey, option.ToString());
        Apply(option);
    }

    public static void Apply(AppThemeOption option)
    {
        var app = Application.Current;
        if (app is null) return;

        app.UserAppTheme = option switch
        {
            AppThemeOption.Light  => AppTheme.Light,
            AppThemeOption.Dark   => AppTheme.Dark,
            _                     => AppTheme.Unspecified, // System (follow OS)
        };
    }
}