using System;
using Microsoft.Maui.Controls;
using DragonTools.Pages.Tools.RandomPicker;
using DragonTools.Pages.Settings;
using DragonTools.Pages.Tools.Notes;
using DragonTools.Pages.Tools.Todo;
using DragonTools.Navigation; // added

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
                    _ = await SafeNavigation.PushAsync(Navigation, new RandomPickerPage());
                    break;
                case "todo":
                    _ = await SafeNavigation.PushAsync(Navigation, new TodoPage());
                    break;
                case "notes":
                    _ = await SafeNavigation.PushAsync(Navigation, new NotesPage());
                    break;
            }
        }
    }

    private async void OpenSettings_Clicked(object sender, EventArgs e)
    {
        _ = await SafeNavigation.PushAsync(Navigation, new SettingsPage());
    }
}
