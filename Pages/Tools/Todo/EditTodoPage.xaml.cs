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
}
