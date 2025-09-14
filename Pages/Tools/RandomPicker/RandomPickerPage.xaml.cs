using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using DragonTools.Interfaces;

namespace DragonTools.Pages.Tools.RandomPicker;

public partial class RandomPickerPage : ContentPage
{
    private RandomPickerVm ViewModel => (RandomPickerVm)BindingContext;

    public RandomPickerPage()
    {
        InitializeComponent();
        BindingContext = new RandomPickerVm();
        // Ensure default tab state
        SetActiveTab("Manage");
        UpdateManageTabStats();
        UpdateStatsDisplay();

        // Set default paging options to 5
        ViewModel.PageSize = 5;
    }

    // Tabs
    private void ManageTab_Clicked(object sender, EventArgs e) => SetActiveTab("Manage");
    private void EntriesTab_Clicked(object sender, EventArgs e) => SetActiveTab("Entries");
    private void RollTab_Clicked(object sender, EventArgs e)
    {
        SetActiveTab("Roll");
        UpdateStatsDisplay();
    }

    private void SetActiveTab(string tab)
    {
        // Button styles
        ManageTab.Style = (Style)Resources["TabButtonStyle"];
        EntriesTab.Style = (Style)Resources["TabButtonStyle"];
        RollTab.Style = (Style)Resources["TabButtonStyle"];

        // Content visibility
        ManageContent.IsVisible = false;
        EntriesContent.IsVisible = false;
        RollContent.IsVisible = false;

        switch (tab)
        {
            case "Manage":
                ManageTab.Style = (Style)Resources["ActiveTabButtonStyle"];
                ManageContent.IsVisible = true;
                UpdateManageTabStats();
                break;
            case "Entries":
                EntriesTab.Style = (Style)Resources["ActiveTabButtonStyle"];
                EntriesContent.IsVisible = true;
                break;
            case "Roll":
                RollTab.Style = (Style)Resources["ActiveTabButtonStyle"];
                RollContent.IsVisible = true;
                break;
        }
    }

    private void UpdateStatsDisplay()
    {
        if (TotalItemsLabel != null)
            TotalItemsLabel.Text = ViewModel.Items.Count.ToString();
    }

    private void UpdateManageTabStats()
    {
        if (ManageItemsCount != null)
            ManageItemsCount.Text = ViewModel.Items.Count.ToString();
        if (ListStatus != null)
            ListStatus.Text = ViewModel.Items.Count > 0 ? "Ready" : "Empty";
    }

    // Manage - toolbar
    private async void OpenSettings_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Settings.SettingsPage());
    }

    // Manage - list actions
    private async void RefreshLists_Clicked(object sender, EventArgs e)
    {
        await ViewModel.LoadSavedListsAsync();
        UpdateManageTabStats();
        await DisplayAlert("Refresh", "Saved lists refreshed.", "OK");
    }

    private async void CreateList_Clicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("New List", "Enter list name:", "Next", "Cancel", placeholder: "My List");
        if (string.IsNullOrWhiteSpace(name)) return;

        var type = await DisplayActionSheet("Select List Type", "Cancel", null, "Normal", "Weighted");
        if (type == "Cancel" || string.IsNullOrWhiteSpace(type)) return;

        ViewModel.CreateNewList(name.Trim(), type == "Weighted");
        UpdateManageTabStats();
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ListName) || ViewModel.ListName == "New List")
        {
            var name = await DisplayPromptAsync("Save List", "Enter a name:", "Save", "Cancel", placeholder: "My List");
            if (string.IsNullOrWhiteSpace(name)) return;
            ViewModel.ListName = name.Trim();
        }

        var ok = await ViewModel.SaveListAsync(ViewModel.ListName);
        await DisplayAlert(ok ? "Saved" : "Error", ok ? $"Saved '{ViewModel.ListName}'." : "Save failed.", "OK");
        UpdateManageTabStats();
    }

    private async void DeleteList_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ListName) || ViewModel.ListName == "New List")
        {
            await DisplayAlert("Cannot delete", "'New List' cannot be deleted.", "OK");
            return;
        }
        var confirm = await DisplayAlert("Delete", $"Delete '{ViewModel.ListName}'?", "Delete", "Cancel");
        if (!confirm) return;

        var ok = await ViewModel.DeleteListAsync(ViewModel.ListName);
        if (ok)
        {
            ViewModel.ListName = "New List";
            ViewModel.ClearItems();
            ViewModel.SelectedList = string.Empty; // clear Picker selection
        }
        await DisplayAlert(ok ? "Deleted" : "Error", ok ? "List deleted." : "Delete failed.", "OK");
        UpdateManageTabStats();
    }

    private async void EditList_Clicked(object sender, EventArgs e)
    {
        // Step 1: Name
        var newName = await DisplayPromptAsync("Edit List", "Enter new name:", "Next", "Cancel", initialValue: ViewModel.ListName);
        if (string.IsNullOrWhiteSpace(newName)) return;
        newName = newName.Trim();

        // Step 2: Type
        var type = await DisplayActionSheet("Select List Type", "Cancel", null, "Normal", "Weighted");
        if (string.IsNullOrWhiteSpace(type) || type == "Cancel") return;
        var isWeighted = type == "Weighted";

        // Step 3: Default Weight (only if weighted)
        if (isWeighted)
        {
            var weightStr = await DisplayPromptAsync(
                "Default Weight",
                "Enter default weight for entries without explicit [weight]:",
                "OK",
                "Skip",
                initialValue: ViewModel.DefaultWeight.ToString(),
                keyboard: Keyboard.Numeric);
            if (!string.IsNullOrWhiteSpace(weightStr) && int.TryParse(weightStr, out var w))
            {
                ViewModel.DefaultWeight = Math.Max(1, w);
            }
        }

        // Apply changes and persist
        var oldName = ViewModel.ListName;
        ViewModel.ListName = newName;
        ViewModel.IsWeightedMode = isWeighted;

        if (!string.Equals(oldName, "New List", StringComparison.OrdinalIgnoreCase))
        {
            var saved = await ViewModel.SaveListAsync(ViewModel.ListName);
            if (saved && !string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                await ViewModel.DeleteListAsync(oldName);
            }
        }
        UpdateManageTabStats();
        UpdateStatsDisplay();
    }

    private void NavigateToEntriesTab(object sender, EventArgs e)
    {
        SetActiveTab("Entries");
    }

    private async void SavedList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (SavedListPicker.SelectedItem is string selected)
        {
            // Auto-load the selected list
            var ok = await ViewModel.LoadListAsync(selected);
            if (ok)
            {
                // Update the Current List Info section
                UpdateManageTabStats();
                UpdateStatsDisplay();
            }
        }
    }

    // Entries
    private void AddBulkItems_Clicked(object sender, EventArgs e)
    {
        ViewModel.AddBulkItems();
        UpdateManageTabStats();
        UpdateStatsDisplay();
    }

    private void OptionsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.FilterItems();
    }

    private async void EditItem_Clicked(object sender, EventArgs e)
    {
        if (sender is Button b && b.BindingContext is IChoice item)
        {
            var text = await DisplayPromptAsync("Edit Item", "Update text:", "Save", "Cancel", initialValue: item.Entry);
            if (!string.IsNullOrWhiteSpace(text))
            {
                item.Entry = text.Trim();
                ViewModel.UpdatePagedItems();
                UpdateStatsDisplay();
            }
        }
    }

    private async void DeleteItem_Clicked(object sender, EventArgs e)
    {
        if (sender is Button b && b.BindingContext is IChoice item)
        {
            var confirm = await DisplayAlert("Delete Item", $"Delete '{item.Entry}'?", "Delete", "Cancel");
            if (confirm)
            {
                ViewModel.RemoveItem(item);
                UpdateManageTabStats();
                UpdateStatsDisplay();
            }
        }
    }

    private void PrevPage_Clicked(object sender, EventArgs e) => ViewModel.PreviousPage();
    private void NextPage_Clicked(object sender, EventArgs e) => ViewModel.NextPage();

    // Roll
    private async void Roll_Clicked(object sender, EventArgs e)
    {
        var picked = ViewModel.RollRandom();
        if (picked == null)
        {
            await DisplayAlert("No Items", "Add items first.", "OK");
            return;
        }
        LastResultLabel.Text = picked.Entry;
        await DisplayAlert("Random Pick", $"Result: {picked.Entry}", "OK");
    }
}
