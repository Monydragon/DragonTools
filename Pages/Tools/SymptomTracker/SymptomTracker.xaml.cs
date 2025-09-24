using System;
using DragonTools.Pages.Home;
using DragonTools.Helpers;
using DragonTools.Enums;

namespace DragonTools.Pages.Tools.SymptomTracker;

public partial class SymptomTracker : ContentPage
{
    public SymptomTracker()
    {
        InitializeComponent();
        BindingContext = new SymptomTrackerViewModel();
    }


    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (BindingContext is SymptomTrackerViewModel vm)
        {
            vm.SaveEntries();
            await DisplayAlertAsync("Saved", "Your symptom entries have been saved.", "OK");
        }
    }

    private async void OnViewEntriesClicked(object sender, EventArgs e)
    {
        if (BindingContext is SymptomTrackerViewModel vm)
        {
            await Navigation.PushAsync(new SymptomEntriesPage(vm.Journal));
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new HomePage());
    }
}