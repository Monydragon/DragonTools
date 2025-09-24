using DragonTools.Models;
using DragonTools.Services;
using Microsoft.Maui.Controls.Xaml;

namespace DragonTools.Pages.Tools.Todo;

[XamlCompilation(XamlCompilationOptions.Skip)]
public partial class TodoPage : ContentPage
{
    public TodoVm Vm { get; } = new();
    bool _loaded;

    public TodoPage()
    {
        InitializeComponent();
        BindingContext = Vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (!_loaded)
            {
                _loaded = true;
                await ServiceLocator.Todos.LoadAsync();
            }
            Vm.Refresh();
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void AddTask_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new CreateTodoPage());
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }

    private async void AddSubtask_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem parent)
            {
                await Navigation.PushAsync(new CreateTodoPage(parent));
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }

    private void Complete_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                ServiceLocator.Todos.SetComplete(item, e.Value);
                _ = ServiceLocator.Todos.SaveAsync();
                Vm.Refresh();
            }
        }
        catch (Exception ex)
        {
            _ = this.DisplayAlertAsync("Error updating task", ex.Message, "OK");
        }
    }

    private void ToggleExpand_Clicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
        {
            item.IsExpanded = !item.IsExpanded;
            Vm.Refresh();
        }
    }

    private async void EditTask_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                await Navigation.PushAsync(new EditTodoPage(item));
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }

    private async void ConfirmDelete_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                var ok = await this.DisplayAlertAsync("Delete task?", $"Are you sure you want to delete '{item.Title}'?", "Delete", "Cancel");
                if (ok)
                {
                    await ServiceLocator.Todos.RemoveAndSaveAsync(item);
                    Vm.Refresh();
                }
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    private async void ConfirmDelete_SwipeInvoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is TodoItem item)
            {
                var ok = await this.DisplayAlertAsync("Delete task?", $"Are you sure you want to delete '{item.Title}'?", "Delete", "Cancel");
                if (ok)
                {
                    await ServiceLocator.Todos.RemoveAndSaveAsync(item);
                    Vm.Refresh();
                }
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    private async void EditTask_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                await Navigation.PushAsync(new EditTodoPage(item));
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }
}
