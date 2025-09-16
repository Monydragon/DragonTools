using DragonTools.Models.Symptom_Tracker;

namespace DragonTools.Pages.Tools.SymptomTracker;

public partial class SymptomEntriesPage : ContentPage
{
    public SymptomEntriesPage(SymptomJournal journal)
    {
        InitializeComponent();
        BindingContext = new SymptomEntriesViewModel(journal);
    }

    private async void OnDeleteEntryClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SymptomHandler entry)
        {
            bool confirm = await DisplayAlert("Delete Entry", "Are you sure you want to delete this entry?", "Delete", "Cancel");
            if (confirm && BindingContext is SymptomEntriesViewModel vm)
            {
                vm.DeleteEntry(entry);
            }
        }
    }

    private async void OnDeleteEntryGroupClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SymptomEntryGroup group)
        {
            bool confirm = await DisplayAlertAsync("Delete All Entries", $"Are you sure you want to delete all entries for {group.DisplayDate}?", "Delete All", "Cancel");
            if (confirm && BindingContext is SymptomEntriesViewModel vm)
            {
                vm.DeleteEntryGroup(group);
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
