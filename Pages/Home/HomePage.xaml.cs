using DragonTools.Pages.Tools.RandomPicker;
using DragonTools.Pages.Tools.SymptomTracker;

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

    private async void OpenSymptomTracker_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SymptomTracker());
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