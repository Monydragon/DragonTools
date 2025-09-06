using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using DragonTools.Pages.Tools.RandomPicker;
using DragonTools.Pages.Settings;

namespace DragonTools.Pages.Home;

public partial class HomePage : ContentPage
{
    public ObservableCollection<ToolTile> Tools { get; } = new();

    public HomePage()
    {
        InitializeComponent();
        BindingContext = this;

        // Add tiles here (small boxes, scroll down)
        Tools.Add(new ToolTile("random_picker", "🎲", "Random Picker"));
        // Add more tiles:
        // Tools.Add(new ToolTile("another_tool", "🧰", "Another Tool"));
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
                // case "another_tool": await Navigation.PushAsync(new AnotherToolPage()); break;
            }
        }
    }

    private async void OpenSettings_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }
}

public sealed class ToolTile
{
    public string Key { get; }
    public string Emoji { get; }
    public string Title { get; }
    public string Display => $"{Emoji}  {Title}";

    public ToolTile(string key, string emoji, string title)
    {
        Key = key; Emoji = emoji; Title = title;
    }
}