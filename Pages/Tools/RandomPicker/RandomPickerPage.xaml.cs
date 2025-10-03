using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using DragonTools.Interfaces;
using DragonTools.Services;
using System.Text.RegularExpressions;
using DragonTools.Pages.Components;
using DragonTools.Pages.Tools.RandomPicker.Voice;
using Microsoft.Maui.Devices; // added
using DragonTools.Navigation; // added

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
    private void ManageTab_Clicked(object? sender, EventArgs? e) => SetActiveTab("Manage");
    private async void EntriesTab_Clicked(object? sender, EventArgs? e)
    {
        if (!ViewModel.HasActiveList)
        {
            await DisplayAlertAsync("Select a list", "Please select or create a list first.", "OK");
            return;
        }
        SetActiveTab("Entries");
    }
    private async void RollTab_Clicked(object? sender, EventArgs? e)
    {
        if (!ViewModel.CanRoll)
        {
            await DisplayAlertAsync("Add items", "Please add items to the selected list before rolling.", "OK");
            return;
        }
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
        // Ensure labels remain data-bound to the ViewModel so they update automatically
        if (ManageItemsCount != null)
            ManageItemsCount.SetBinding(Label.TextProperty, new Binding("Items.Count"));
        if (ListStatus != null)
            ListStatus.SetBinding(Label.TextProperty, new Binding("ListStatusText"));
    }

    // Manage - toolbar
    private async void OpenSettings_Clicked(object? sender, EventArgs? e)
    {
        _ = await SafeNavigation.PushAsync(Navigation, new Settings.SettingsPage());
    }

    private async void RefreshLists_Clicked(object? sender, EventArgs? e)
    {
        await ViewModel.LoadSavedListsAsync();
        UpdateManageTabStats();
        await DisplayAlertAsync("Refresh", "Saved lists refreshed.", "OK");
    }

    private async void CreateList_Clicked(object? sender, EventArgs? e)
    {
        var name = await DisplayPromptAsync("New List", "Enter list name:", "Next", "Cancel", placeholder: "My List");
        if (string.IsNullOrWhiteSpace(name)) return;

        var type = await DisplayActionSheetAsync("Select List Type", "Cancel", null, "Normal", "Weighted");
        if (type == "Cancel" || string.IsNullOrWhiteSpace(type)) return;

        ViewModel.CreateNewList(name.Trim(), type == "Weighted");
        ViewModel.NotifyInfoCard();
        UpdateManageTabStats();
    }

    private async void DeleteList_Clicked(object? sender, EventArgs? e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ListName) || ViewModel.ListName == "New List")
        {
            await DisplayAlertAsync("Cannot delete", "'New List' cannot be deleted.", "OK");
            return;
        }
        var confirm = await DisplayAlertAsync("Delete", $"Delete '{ViewModel.ListName}'?", "Delete", "Cancel");
        if (!confirm) return;

        var ok = await ViewModel.DeleteListAsync(ViewModel.ListName);
        if (ok)
        {
            ViewModel.ListName = "New List";
            ViewModel.ClearItems();
            ViewModel.SelectedList = string.Empty; // clear Picker selection
        }
        ViewModel.NotifyInfoCard();
        await DisplayAlertAsync(ok ? "Deleted" : "Error", ok ? "List deleted." : "Delete failed.", "OK");
        UpdateManageTabStats();
    }

    private async void EditList_Clicked(object? sender, EventArgs? e)
    {
        // Step 1: Name
        var newName = await DisplayPromptAsync("Edit List", "Enter new name:", "Next", "Cancel", initialValue: ViewModel.ListName);
        if (string.IsNullOrWhiteSpace(newName)) return;
        newName = newName.Trim();

        // Step 2: Type
        var type = await DisplayActionSheetAsync("Select List Type", "Cancel", null, "Normal", "Weighted");
        if (string.IsNullOrWhiteSpace(type) || type == "Cancel") return;
        var isWeighted = type == "Weighted";

        // Step 3: Default Weight (only if weighted)
        if (isWeighted)
        {
            var weightStr = await DisplayPromptAsync(
                "Default Weight",
                "Enter default weight for entries with explicit [weight]:",
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
        ViewModel.NotifyInfoCard();
        UpdateManageTabStats();
        UpdateStatsDisplay();
    }

    private async void NavigateToEntriesTab(object? sender, EventArgs? e)
    {
        if (!ViewModel.HasActiveList)
        {
            await DisplayAlertAsync("Select a list", "Please select or create a list first.", "OK");
            return;
        }
        SetActiveTab("Entries");
    }

    private async void SavedList_SelectedIndexChanged(object? sender, EventArgs? e)
    {
        if (SavedListPicker.SelectedItem is string selected)
        {
            var ok = await ViewModel.LoadListAsync(selected);
            if (ok)
            {
                UpdateManageTabStats();
                UpdateStatsDisplay();
            }
        }
    }

    // Entries
    private void AddBulkItems_Clicked(object? sender, EventArgs? e)
    {
        ViewModel.AddBulkItems();
        UpdateManageTabStats();
        UpdateStatsDisplay();
    }

    private void OptionsSearch_TextChanged(object? sender, TextChangedEventArgs? e)
    {
        ViewModel.FilterItems();
        UpdateManageTabStats();
    }

    private async void EditItem_Clicked(object? sender, EventArgs? e)
    {
        if (sender is Button b && b.BindingContext is IChoice item)
        {
            var newName = await DisplayPromptAsync("Edit Item", "Update name:", "Next", "Cancel", initialValue: item.Entry);
            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();

            if (ViewModel.IsWeightedMode)
            {
                var weightStr = await DisplayPromptAsync(
                    "Edit Weight",
                    "Enter item weight (>= 1):",
                    "Save",
                    "Skip",
                    initialValue: item.Weight.ToString(),
                    keyboard: Keyboard.Numeric);
                if (!string.IsNullOrWhiteSpace(weightStr) && int.TryParse(weightStr, out var w))
                {
                    item.Weight = Math.Max(1, w);
                }
            }

            item.Entry = newName;
            ViewModel.UpdatePagedItems();
            UpdateManageTabStats();
            UpdateStatsDisplay();
        }
    }

    private async void DeleteItem_Clicked(object? sender, EventArgs? e)
    {
        if (sender is Button b && b.BindingContext is IChoice item)
        {
            var confirm = await DisplayAlertAsync("Delete Item", $"Delete '{item.Entry}'?", "Delete", "Cancel");
            if (confirm)
            {
                ViewModel.RemoveItem(item);
                UpdateManageTabStats();
                UpdateStatsDisplay();
            }
        }
    }

    private async void Roll_Clicked(object? sender, EventArgs? e)
    {
        var picked = ViewModel.RollRandom();
        if (picked == null)
        {
            await DisplayAlertAsync("No Items", "Add items first.", "OK");
            return;
        }
        LastResultLabel.Text = picked.Entry;
        await DisplayAlertAsync("Random Pick", $"Result: {picked.Entry}", "OK");
    }

    // Voice: unified voice input that handles both single and bulk adding intelligently
    private async void VoiceInput_Clicked(object? sender, EventArgs? e)
    {
        if (!ViewModel.HasActiveList)
        {
            await DisplayAlertAsync("Select a list", "Please select or create a list first.", "OK");
            return;
        }

        var speech = ServiceHelper.Get<ISpeechToTextService>();
        var granted = await speech.EnsurePermissionsAsync();
        if (!granted)
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                await DisplayAlertAsync(
                    "Speech not available",
                    "Windows speech engine not found. Install a Speech language pack (Settings > Time & language > Speech) and enable Windows Speech Recognition, then try again.",
                    "OK");
            }
            else
            {
                await DisplayAlertAsync("Permission required", "Microphone access is required for voice input.", "OK");
            }
            return;
        }

        var voiceButton = sender as Button;
        var originalText = voiceButton?.Text;
        var originalStyle = voiceButton?.Style;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var modal = new ListeningModalPage(
            "Listening… Speak now",
            ViewModel.IsWeightedMode
                ? "Say items. For weights: 'apple [3]', 'apple weight 3', 'apple with weight 3', '3x apple'."
                : "Say items separated by comma…");
        Action cancelHandler = () => cts.Cancel();
        modal.CancelRequested += cancelHandler;

        try
        {
            if (voiceButton != null)
            {
                voiceButton.Text = "🔴";
                voiceButton.Style = (Style)Resources["PrimaryButtonStyle"];
                voiceButton.IsEnabled = false;
            }

            await Navigation.PushModalAsync(modal, false);

            string currentPartial = string.Empty;
            var final = await speech.ListenWithProgressAsync(
                onPartial: text =>
                {
                    currentPartial = text;
                    MainThread.BeginInvokeOnMainThread(() => modal.UpdateTranscript(text));
                },
                prompt: ViewModel.IsWeightedMode
                    ? "Speak items. For weights: 'apple [3]', 'apple weight 3', 'with weight 3', '3x apple'."
                    : "Speak one item or multiple items separated by comma",
                cancellationToken: cts.Token);

            await Navigation.PopModalAsync(false);

            var text = string.IsNullOrWhiteSpace(final) ? currentPartial : final;

            // Windows fallback: if no text from continuous, try one-shot recognition once
            if (string.IsNullOrWhiteSpace(text) && DeviceInfo.Current.Platform == DevicePlatform.WinUI && !cts.IsCancellationRequested)
            {
                try
                {
                    var once = await speech.ListenOnceAsync(prompt: ViewModel.IsWeightedMode
                        ? "Say an item with optional weight, e.g., 'apple [3]'"
                        : "Say a single item", cancellationToken: cts.Token);
                    if (!string.IsNullOrWhiteSpace(once))
                    {
                        text = once;
                    }
                }
                catch { /* ignore, will fall back to no-speech alert */ }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                var msg = DeviceInfo.Current.Platform == DevicePlatform.WinUI
                    ? "No speech detected. Try speaking closer to the mic. If it ends instantly, open Control Panel > Speech Recognition, click 'Set up microphone' and ensure a Speech language is installed."
                    : "No speech was detected. Please try speaking louder and clearer.";
                await DisplayAlertAsync("No Speech Detected", msg, "OK");
                return;
            }

            // Normalize common words to commas
            var normalized = text
                .Replace(" ,", ",")
                .Replace(", ", ", ")
                .Replace(" comma ", ", ", StringComparison.OrdinalIgnoreCase)
                .Replace(" comma", ",", StringComparison.OrdinalIgnoreCase)
                .Replace("comma ", ", ", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var hasCommas = normalized.Contains(',');
            var hasAnd = normalized.Contains(" and ", StringComparison.OrdinalIgnoreCase);
            var isBulkInput = hasCommas || hasAnd;

            if (isBulkInput)
            {
                var added = new List<string>();
                foreach (var res in VoiceParser.ParseBulk(normalized))
                {
                    var entry = (res.Entry ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    var weight = ViewModel.IsWeightedMode ? (res.Weight ?? 1) : 1;
                    ViewModel.AddItem(entry, weight);
                    added.Add(ViewModel.IsWeightedMode && weight > 1 ? $"{entry} [{weight}]" : entry);
                }

                UpdateManageTabStats();
                UpdateStatsDisplay();

                if (added.Count > 0)
                    await DisplayAlertAsync("✅ Items Added", string.Join(", ", added), "OK");
                else
                    await DisplayAlertAsync("Nothing Added", "Couldn't parse any items.", "OK");
            }
            else
            {
                var res = VoiceParser.ParseSingle(normalized);
                var entry = (res.Entry ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(entry))
                {
                    await DisplayAlertAsync("Not recognized", "Please try again.", "OK");
                    return;
                }

                var weight = ViewModel.IsWeightedMode ? (res.Weight ?? 1) : 1;
                ViewModel.AddItem(entry, weight);
                UpdateManageTabStats();
                UpdateStatsDisplay();

                await DisplayAlertAsync("✅ Item Added", ViewModel.IsWeightedMode && weight > 1 ? $"Added: {entry} (weight: {weight})" : $"Added: {entry}", "OK");
            }
        }
        catch (OperationCanceledException)
        {
            try { await Navigation.PopModalAsync(false); } catch { }
            await DisplayAlertAsync("Voice Input Cancelled", "Voice input was cancelled.", "OK");
        }
        catch (Exception ex)
        {
            try { await Navigation.PopModalAsync(false); } catch { }
            await DisplayAlertAsync("Voice Input Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            modal.CancelRequested -= cancelHandler;
            cts.Dispose();
            if (voiceButton != null)
            {
                voiceButton.Text = originalText;
                voiceButton.Style = originalStyle;
                voiceButton.IsEnabled = true;
            }
        }
    }

    // Pagination event handlers
    private void PrevPage_Clicked(object? sender, EventArgs? e)
    {
        ViewModel.PreviousPage();
    }

    private void NextPage_Clicked(object? sender, EventArgs? e)
    {
        ViewModel.NextPage();
    }
}
