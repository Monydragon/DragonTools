using System;
using Microsoft.Maui.Controls;
using DragonTools.Models;

namespace DragonTools.Pages.Tools.Todo;

public partial class CreateTodoPage : ContentPage
{
    public CreateTodoVm Vm { get; }

    public CreateTodoPage()
    {
        InitializeComponent();
        Vm = new CreateTodoVm();
        BindingContext = Vm;
        SaveBtn.Clicked += Save_Clicked;
        CancelBtn.Clicked += Cancel_Clicked;
    }

    public CreateTodoPage(TodoItem parent)
    {
        InitializeComponent();
        Vm = new CreateTodoVm(parent);
        BindingContext = Vm;
        SaveBtn.Clicked += Save_Clicked;
        CancelBtn.Clicked += Cancel_Clicked;
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        var ok = await Vm.SaveAsync();
        if (!ok)
        {
            await this.DisplayAlertAsync("Missing title", "Please enter a task title.", "OK");
            return;
        }
        await Navigation.PopAsync();
    }

    private async void Cancel_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void AddChecklist_Clicked(object sender, EventArgs e)
    {
        var text = ChecklistEntry?.Text ?? string.Empty;
        Vm.AddChecklistEntry(text);
        if (ChecklistEntry != null) ChecklistEntry.Text = string.Empty;
    }

    private void RemoveChecklist_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ChecklistEntry entry)
        {
            Vm.RemoveChecklistEntry(entry);
        }
    }

    private void AddReminder_Clicked(object sender, EventArgs e)
    {
        Vm.AddReminder();
    }

    private void RemoveReminder_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ReminderEntry entry)
        {
            Vm.RemoveReminder(entry);
        }
    }

    private void SetDifficulty_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string s && Enum.TryParse<TodoDifficulty>(s, true, out var diff))
        {
            Vm.Difficulty = diff;
        }
    }
}
