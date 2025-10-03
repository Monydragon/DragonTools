using DragonTools.Models;
using DragonTools.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System.Threading;
using DragonTools.Navigation; // added

#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
#endif

namespace DragonTools.Pages.Tools.Todo;

// Use compiled XAML for safety/performance
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class TodoPage : ContentPage
{
    // Expose strongly-typed VM referencing the XAML-created BindingContext
    public TodoVm Vm => (TodoVm)BindingContext;
    bool _loaded;
    TodoItem? _lastDeleted;
    TodoItem? _lastDeletedParent;
    int _lastDeletedIndex;
    CancellationTokenSource? _toastCts;

    public TodoPage()
    {
        InitializeComponent();
        // If XAML did not set BindingContext for any reason, fallback.
        if (BindingContext is not TodoVm)
        {
            BindingContext = new TodoVm();
        }

        // Track collection changes to keep a selection valid
        Vm.DisplayItems.CollectionChanged += (_, __) => EnsureSelection();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        if (Handler?.PlatformView is FrameworkElement fe)
        {
            fe.KeyDown -= OnPlatformKeyDown;
            fe.KeyDown += OnPlatformKeyDown;
        }
#endif
    }

#if WINDOWS
    private void OnPlatformKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (Vm.DisplayItems.Count == 0) return;
        int idx = GetSelectedIndex();
        if (e.Key == Windows.System.VirtualKey.Down)
        {
            idx = Math.Min(Vm.DisplayItems.Count - 1, idx + 1);
            SelectIndex(idx);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            idx = Math.Max(0, idx - 1);
            SelectIndex(idx);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Space)
        {
            var item = Vm.DisplayItems[idx];
            _ = ServiceLocator.Todos.SetCompleteAndSaveAsync(item, !item.IsCompleted);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var item = Vm.DisplayItems[idx];
            ToggleExpand(item);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Delete)
        {
            var item = Vm.DisplayItems[idx];
            DeleteItemWithUndo(item);
            e.Handled = true;
        }
    }
#endif

    // --------------- Selection Helpers -----------------
    int GetSelectedIndex()
    {
        for (int i = 0; i < Vm.DisplayItems.Count; i++)
            if (Vm.DisplayItems[i].IsSelected) return i;
        return 0;
    }

    void SelectIndex(int index)
    {
        if (Vm.DisplayItems.Count == 0) return;
        if (index < 0) index = 0;
        if (index >= Vm.DisplayItems.Count) index = Vm.DisplayItems.Count - 1;
        for (int i = 0; i < Vm.DisplayItems.Count; i++)
            Vm.DisplayItems[i].IsSelected = (i == index);
    }

    void EnsureSelection()
    {
        if (Vm.DisplayItems.Count == 0) return;
        if (!Vm.DisplayItems.Any(i => i.IsSelected))
            Vm.DisplayItems[0].IsSelected = true;
    }

    // --------------- Expand / Collapse -----------------
    private void ToggleExpand(TodoItem item)
    {
        bool expanding = !item.IsExpanded;
        item.IsExpanded = expanding;
        if (expanding)
        {
            Vm.ExpandExclusive(item);
            // fire-and-forget animation
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    // Attempt to locate visual elements by walking up from selection if needed
                    // (Safe no-op if not found)
                    await Task.Delay(10); // let layout apply
                }
                catch { }
            });
        }
    }

    private async void AddTask_Clicked(object sender, EventArgs e)
    {
        try
        {
            var ok = await SafeNavigation.PushAsync(Navigation, new CreateTodoPage());
            if (!ok) await DisplayAlertAsync("Navigation error", "Failed to open create page", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }

    private async void AddSubtask_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem parent)
            {
                var ok = await SafeNavigation.PushAsync(Navigation, new CreateTodoPage(parent));
                if (!ok) await DisplayAlertAsync("Navigation error", "Failed to open subtask page", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Navigation error", ex.Message, "OK");
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
            _ = DisplayAlertAsync("Error updating task", ex.Message, "OK");
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
                var ok = await SafeNavigation.PushAsync(Navigation, new EditTodoPage(item));
                if (!ok) await DisplayAlertAsync("Navigation error", "Failed to open edit page", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Navigation error", ex.Message, "OK");
        }
    }

    // --------------- Delete + Undo -----------------
    void DeleteItemWithUndo(TodoItem item)
    {
        // Capture parent & index
        _lastDeletedParent = FindParent(item, out _lastDeletedIndex);
        _lastDeleted = item;
        ServiceLocator.Todos.Remove(item);
        ShowUndoToast($"Deleted '{item.Title}'");
    }

    private async void ConfirmDelete_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                var ok = await DisplayAlertAsync("Delete task?", $"Are you sure you want to delete '{item.Title}'?", "Delete", "Cancel");
                if (ok)
                {
                    DeleteItemWithUndo(item);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    private async void ConfirmDelete_SwipeInvoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is TodoItem item)
            {
                var ok = await DisplayAlertAsync("Delete task?", $"Are you sure you want to delete '{item.Title}'?", "Delete", "Cancel");
                if (ok)
                {
                    DeleteItemWithUndo(item);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    private TodoItem? FindParent(TodoItem target, out int index)
    {
        // root
        index = ServiceLocator.Todos.Tasks.IndexOf(target);
        if (index >= 0) return null;

        foreach (var root in ServiceLocator.Todos.Tasks)
        {
            var parent = FindParentRecursive(root, target, out index);
            if (parent != null) return parent;
        }
        index = -1;
        return null;
    }

    private TodoItem? FindParentRecursive(TodoItem current, TodoItem target, out int index)
    {
        index = current.SubTasks.IndexOf(target);
        if (index >= 0) return current;
        foreach (var child in current.SubTasks)
        {
            var parent = FindParentRecursive(child, target, out index);
            if (parent != null) return parent;
        }
        index = -1;
        return null;
    }

    void ShowUndoToast(string message, int durationMs = 4000)
    {
        try
        {
            ToastMessage.Text = message;
            ToastPanel.IsVisible = true;
            ToastPanel.Opacity = 0;
            _ = ToastPanel.FadeTo(1, 180, Easing.CubicOut);
            _toastCts?.Cancel();
            var cts = new CancellationTokenSource();
            _toastCts = cts;
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    await Task.Delay(durationMs, cts.Token);
                    if (!cts.IsCancellationRequested)
                        await HideToastAsync();
                }
                catch (TaskCanceledException) { }
            });
        }
        catch { }
    }

    async Task HideToastAsync()
    {
        try
        {
            await ToastPanel.FadeTo(0, 180, Easing.CubicIn);
            ToastPanel.IsVisible = false;
        }
        catch { }
    }

    private async void Undo_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            _toastCts?.Cancel();
            if (_lastDeleted != null)
            {
                ServiceLocator.Todos.InsertExisting(_lastDeleted, _lastDeletedParent, _lastDeletedIndex);
                // Reselect reinstated item
                var idx = Vm.DisplayItems.IndexOf(_lastDeleted);
                if (idx >= 0) SelectIndex(idx);
            }
            _lastDeleted = null; _lastDeletedParent = null; _lastDeletedIndex = -1;
            await HideToastAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Undo failed: {ex}");
        }
    }

    private async void CompleteSwipe_Invoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is TodoItem item)
            {
                if (!item.IsCompleted)
                {
                    await ServiceLocator.Todos.SetCompleteAndSaveAsync(item, true);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Swipe complete failed: {ex}");
        }
    }

    private async void UncompleteSwipe_Invoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is TodoItem item)
            {
                if (item.IsCompleted)
                {
                    await ServiceLocator.Todos.SetCompleteAndSaveAsync(item, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Swipe un-complete failed: {ex}");
        }
    }

    private async void Tile_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (sender is BindableObject bo && bo.BindingContext is TodoItem item)
            {
                await ServiceLocator.Todos.SetCompleteAndSaveAsync(item, !item.IsCompleted);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Tile toggle failed: {ex}");
        }
    }

    private void ExpandCollapse_Clicked(object sender, EventArgs e)
    {
        try
        {
            TodoItem? item = GetItemFromSender(sender);
            if (item == null) return;
            ToggleExpand(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Todo] Expand/collapse failed: {ex}");
        }
    }

    TodoItem? GetItemFromSender(object sender)
    {
        if (sender is BindableObject bo && bo.BindingContext is TodoItem t) return t;
        if (sender is Element el)
        {
            var cur = el.Parent;
            while (cur != null)
            {
                if (cur.BindingContext is TodoItem ti) return ti;
                cur = cur.Parent;
            }
        }
        return null;
    }
}
