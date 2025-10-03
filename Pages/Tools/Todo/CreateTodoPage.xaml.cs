using System;
using Microsoft.Maui.Controls;
using DragonTools.Models;
using DragonTools.Navigation; // added
using DragonTools.Services; // for ServiceLocator

namespace DragonTools.Pages.Tools.Todo;

public partial class CreateTodoPage : ContentPage
{
    public CreateTodoVm Vm { get; }
    bool _saving;

    public CreateTodoPage()
    {
        InitializeComponent();
        Vm = new CreateTodoVm();
        BindingContext = Vm;
        // SaveBtn.Clicked += Save_Clicked;
        // CancelBtn.Clicked += Cancel_Clicked;
    }

    public CreateTodoPage(TodoItem parent)
    {
        InitializeComponent();
        Vm = new CreateTodoVm(parent);
        BindingContext = Vm;
        // SaveBtn.Clicked += Save_Clicked;
        // CancelBtn.Clicked += Cancel_Clicked;
    }

    private async void Save_Clicked(object? sender, EventArgs e)
    {
        if (_saving) return;
        _saving = true;
        try
        {
            // Build item without mutating service yet (or without raising Changed)
            if (!Vm.TryBuildItem(out var item) || item == null)
            {
                await DisplayAlertAsync("Missing title", "Please enter a task title.", "OK");
                return;
            }

            // Add to service without raising Changed so CollectionView on previous page is untouched until after pop
            await ServiceLocator.Todos.AddDeferredAsync(item, Vm.Parent);

            // Navigate back before triggering Changed / Recompute
            var popped = await SafeNavigation.PopAsync(Navigation, this);

            // After navigation completes, schedule a save + notify (Changed) on UI thread with slight delay to allow layout settle
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(16); // one frame (~60fps)
                    await ServiceLocator.Todos.SaveAndNotifyAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Todo] Deferred save failed: {ex}");
                }
            });

            System.Diagnostics.Debug.WriteLine($"[Todo] Deferred create queued (popped={popped}) Title='{item.Title}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Save_Clicked exception: {ex}");
            await DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
        finally { _saving = false; }
    }

    private async void Cancel_Clicked(object? sender, EventArgs e)
    {
        _ = await SafeNavigation.PopAsync(Navigation, this);
    }

    private void AddChecklist_Clicked(object? sender, EventArgs e)
    {
        var text = ChecklistInput?.Text ?? string.Empty;
        Vm.AddChecklistEntry(text);
        if (ChecklistInput != null) ChecklistInput.Text = string.Empty;
    }

    private void RemoveChecklist_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ChecklistEntry entry)
        {
            Vm.RemoveChecklistEntry(entry);
        }
    }

    private void AddReminder_Clicked(object? sender, EventArgs e)
    {
        var title = ReminderInput?.Text ?? string.Empty;
        var when = DateTime.Now.AddHours(1); // Default to 1 hour from now, can be improved with a picker
        Vm.AddReminderEntry(title, when);
        if (ReminderInput != null) ReminderInput.Text = string.Empty;
    }

    private void RemoveReminder_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ReminderEntry entry)
        {
            Vm.RemoveReminderEntry(entry);
        }
    }

    private void SetDifficulty_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string s && Enum.TryParse<TodoDifficulty>(s, true, out var diff))
        {
            Vm.Difficulty = diff;
        }
    }
}
