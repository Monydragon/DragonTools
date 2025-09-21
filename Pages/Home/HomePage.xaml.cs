using System;
using Microsoft.Maui.Controls;
using DragonTools.Pages.Tools.RandomPicker;
using DragonTools.Pages.Settings;
using DragonTools.Pages.Tools.Todo;

namespace DragonTools.Pages.Home;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        // No extra setup needed; tiles are defined in XAML.
    }

    private async void Tile_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string key)
        {
            switch (key)
            {
                case "random_picker":
                    await Navigation.PushAsync(new RandomPickerPage());
                    break;
                case "todo":
                    await Navigation.PushAsync(new TodoPage());
                    break;
            }
        }
    }

    private async void OpenSettings_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }
}
