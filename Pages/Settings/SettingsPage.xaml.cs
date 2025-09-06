using System;
using Microsoft.Maui.Controls;

namespace DragonTools.Pages.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var opt = ThemeManager.GetThemeOption();
        SystemThemeRb.IsChecked = opt == AppThemeOption.System;
        LightThemeRb.IsChecked  = opt == AppThemeOption.Light;
        DarkThemeRb.IsChecked   = opt == AppThemeOption.Dark;
    }

    private void ThemeRadio_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if ((sender as RadioButton) is not { IsChecked: true } rb) return;

        if      (rb == SystemThemeRb) ThemeManager.SetTheme(AppThemeOption.System);
        else if (rb == LightThemeRb)  ThemeManager.SetTheme(AppThemeOption.Light);
        else if (rb == DarkThemeRb)   ThemeManager.SetTheme(AppThemeOption.Dark);
    }
}