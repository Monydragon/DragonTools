using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace DragonTools.Pages.Tools.Todo;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class EditTodoPage : ContentPage
{
    public EditTodoVm Vm { get; }

    public EditTodoPage(DragonTools.Models.TodoItem item)
    {
        InitializeComponent();
        Vm = new EditTodoVm(item);
        BindingContext = Vm;
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

    private void AddChecklist_Clicked(object? sender, EventArgs e)
    {
        var text = ChecklistInput?.Text ?? string.Empty;
        Vm.AddChecklistEntry(text);
        if (ChecklistInput != null) ChecklistInput.Text = string.Empty;
    }

    private void RemoveChecklist_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is DragonTools.Models.ChecklistEntry entry)
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
        if (sender is Button btn && btn.CommandParameter is DragonTools.Models.ReminderEntry entry)
        {
            Vm.RemoveReminderEntry(entry);
        }
    }
}
