using DragonTools.Models;
using DragonTools.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace DragonTools.Pages.Tools.Todo;

// Use compiled XAML for safety/performance
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class TodoPage : ContentPage
{
    // Expose strongly-typed VM referencing the XAML-created BindingContext
    public TodoVm Vm => (TodoVm)BindingContext;
    bool _loaded;

    public TodoPage()
    {
        InitializeComponent();
        // If XAML did not set BindingContext for any reason, fallback.
        if (BindingContext is not TodoVm)
        {
            BindingContext = new TodoVm();
        }
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

    private async void Complete_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        try
        {
            if (sender is not BindableObject bo) return;
            if (bo.BindingContext is not TodoItem item) return;

            // If the value is already what we're setting, ignore spurious event
            if (item.IsCompleted == e.Value)
                return;

            // Use consolidated service call (fires Changed once per logical change + save)
            await ServiceLocator.Todos.SetCompleteAndSaveAsync(item, e.Value);
            // No explicit Vm.Refresh(); the service Changed event triggers recompute.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Error completing task {ex}");
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
