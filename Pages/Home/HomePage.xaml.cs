using DragonTools.Pages.Tools.RandomPicker;
using DragonTools.Pages.Tools.SymptomTracker;

namespace DragonTools.Pages.Home;

public partial class HomePage : ContentPage
{
    
    public HomePage() => InitializeComponent();

    private async void OpenRandomPicker_Clicked(object? sender, EventArgs e)
    {
        // If you use Shell, register route and navigate:
        // await Shell.Current.GoToAsync(nameof(RandomPickerPage));

        // If you use NavigationPage:
        await Navigation.PushAsync(new RandomPickerPage());
    }

    private async void OpenSymptomTracker_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SymptomTracker());
    }
}