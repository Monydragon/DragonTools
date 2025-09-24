using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DragonTools.Models;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using System.Linq;

namespace DragonTools.Services;

public sealed class TodoService
{
    private readonly INotificationService _notification;

    public TodoService(INotificationService notification)
    {
        _notification = notification;
    }

    // Root-level tasks (top of the tree)
    public ObservableCollection<TodoItem> Tasks { get; } = new();

    // Raised whenever the underlying collection or item state changes and
    // view models may need to recompute derived data (flatten/sort/filter)
    public event EventHandler? Changed;

    public IEnumerable<TodoItem> Roots => Tasks;

    // Add a task either to roots or as a child of 'parent' (depth capped at 3)
    public async Task AddAsync(TodoItem item, TodoItem? parent = null)
    {
        if (parent == null)
        {
            item.Depth = 1;
            Tasks.Add(item);
        }
        else
        {
            item.Depth = Math.Min(parent.Depth + 1, 3);
            parent.SubTasks.Add(item);
        }

        // Schedule due notification
        if (item.Due is DateTime due && due.ToUniversalTime() > DateTime.UtcNow)
        {
            await _notification.ScheduleDueNotificationAsync(item.Id, item.Title, item.Note ?? "Task due", due.ToUniversalTime());
        }
        // Schedule reminder notifications
        foreach (var r in item.Reminders)
        {
            var when = r.When;
            var rUtc = when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime();
            if (rUtc > DateTime.UtcNow)
            {
                var rid = MakeReminderGuid(item.Id, rUtc);
                await _notification.ScheduleDueNotificationAsync(rid, item.Title, r.Title ?? (item.Note ?? "Reminder"), rUtc);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Toggle completion flag
    public void ToggleComplete(TodoItem item)
    {
        if (item == null) return;
        item.IsCompleted = !item.IsCompleted;
        item.UpdatedAt = DateTime.UtcNow;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Explicitly set completion state
    public void SetComplete(TodoItem item, bool value)
    {
        if (item == null) return;
        if (item.IsCompleted == value) return;
        item.IsCompleted = value;
        item.UpdatedAt = DateTime.UtcNow;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Remove an item from the tree (roots first, then recursive descent)
    public void Remove(TodoItem item)
    {
        if (item == null) return;

        if (Tasks.Remove(item))
        {
            // cancel due
            _ = _notification.CancelNotificationAsync(item.Id);
            // cancel reminders
            foreach (var r in item.Reminders)
            {
                var when = r.When;
                var rid = MakeReminderGuid(item.Id, when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime());
                _ = _notification.CancelNotificationAsync(rid);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (var root in Tasks)
        {
            if (RemoveFrom(root, item))
            {
                _ = _notification.CancelNotificationAsync(item.Id);
                foreach (var r in item.Reminders)
                {
                    var when = r.When;
                    var rid = MakeReminderGuid(item.Id, when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime());
                    _ = _notification.CancelNotificationAsync(rid);
                }
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    static Guid MakeReminderGuid(Guid taskId, DateTime reminderUtc)
    {
        var bytes = taskId.ToByteArray();
        var tb = BitConverter.GetBytes(reminderUtc.Ticks);
        for (int i = 0; i < tb.Length && i < bytes.Length; i++) bytes[i] ^= tb[i];
        return new Guid(bytes);
    }

    private static bool RemoveFrom(TodoItem parent, TodoItem target)
    {
        if (parent.SubTasks.Remove(target)) return true;

        // Copy to avoid modifying while enumerating
        var snapshot = parent.SubTasks.ToList();
        foreach (var child in snapshot)
        {
            if (RemoveFrom(child, target)) return true;
        }
        return false;
    }

    static readonly string SaveFilePath = Path.Combine(FileSystem.AppDataDirectory, "todos.json");

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SaveFilePath)) return;
            var json = await File.ReadAllTextAsync(SaveFilePath);

            // First, deserialize into current model
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var items = JsonSerializer.Deserialize<List<DragonTools.Models.TodoItem>>(json, options);
            if (items == null) items = new List<TodoItem>();

            // Migrate legacy shapes if needed (checklist as strings, reminders as array of date-times)
            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int idx = 0;
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (idx < items.Count) MigrateLegacy(el, items[idx]);
                        idx++;
                    }
                }
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tasks.Clear();
                foreach (var item in items)
                {
                    FixupDepth(item, 1);
                    Tasks.Add(item);
                }
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }
        catch
        {
            // ignore
        }
    }

    static void MigrateLegacy(JsonElement src, DragonTools.Models.TodoItem dest)
    {
        try
        {
            // Checklist migration
            if (src.TryGetProperty("Checklist", out var checklistEl) && checklistEl.ValueKind == JsonValueKind.Array)
            {
                if (checklistEl.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String))
                {
                    // Legacy list of strings
                    foreach (var sEl in checklistEl.EnumerateArray())
                    {
                        if (sEl.ValueKind == JsonValueKind.String)
                        {
                            var txt = sEl.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(txt))
                                dest.Checklist.Add(new DragonTools.Models.ChecklistEntry { Text = txt, IsDone = false });
                        }
                    }
                }
            }
            // Reminders migration (skipped to avoid legacy type shape conflicts)
            // If needed later, convert string array of datetimes to ReminderEntry in a versioned migration.

            // Recurse into subtasks
            if (src.TryGetProperty("SubTasks", out var subEl) && subEl.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var child in subEl.EnumerateArray())
                {
                    if (i < dest.SubTasks.Count)
                        MigrateLegacy(child, dest.SubTasks[i]);
                    i++;
                }
            }
        }
        catch
        {
            // best-effort migration
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var snapshot = Tasks.ToList();
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath)!);
            await using var fs = File.Create(SaveFilePath);
            await JsonSerializer.SerializeAsync(fs, snapshot, options).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
        finally
        {
            // Inform listeners so they can re-group/sort if needed
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    // Reschedule all notifications for an item (due + reminders)
    public async Task RescheduleNotificationsAsync(DragonTools.Models.TodoItem item)
    {
        if (item == null) return;
        // Cancel existing
        _ = _notification.CancelNotificationAsync(item.Id);
        foreach (var r in item.Reminders)
        {
            var when = r.When;
            var rid = MakeReminderGuid(item.Id, when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime());
            _ = _notification.CancelNotificationAsync(rid);
        }
        // Schedule due
        if (item.Due is DateTime due && due.ToUniversalTime() > DateTime.UtcNow)
        {
            await _notification.ScheduleDueNotificationAsync(item.Id, item.Title, item.Note ?? "Task due", due.ToUniversalTime());
        }
        // Schedule reminders
        foreach (var r in item.Reminders)
        {
            var when = r.When;
            var rUtc = when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime();
            if (rUtc > DateTime.UtcNow)
            {
                var rid = MakeReminderGuid(item.Id, rUtc);
                await _notification.ScheduleDueNotificationAsync(rid, item.Title, r.Title ?? (item.Note ?? "Reminder"), rUtc);
            }
        }
    }

    static void FixupDepth(DragonTools.Models.TodoItem item, int depth)
    {
        item.Depth = depth;
        if (item.SubTasks == null) return;
        foreach (var child in item.SubTasks)
            FixupDepth(child, Math.Min(depth + 1, 3));
    }

    // Modify existing mutators to auto-save
    public async Task AddAndSaveAsync(DragonTools.Models.TodoItem item, DragonTools.Models.TodoItem? parent = null)
    {
        await AddAsync(item, parent);
        await SaveAsync();
    }

    public async Task RemoveAndSaveAsync(DragonTools.Models.TodoItem item)
    {
        Remove(item);
        await SaveAsync();
    }

    public async Task SetCompleteAndSaveAsync(DragonTools.Models.TodoItem item, bool value)
    {
        SetComplete(item, value);
        await SaveAsync();
    }
}
