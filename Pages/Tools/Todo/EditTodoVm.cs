using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DragonTools.Models;
using DragonTools.Services;

namespace DragonTools.Pages.Tools.Todo;

public sealed class EditTodoVm : INotifyPropertyChanged
{
    private readonly TodoItem _item;
    private readonly TodoService _service = ServiceLocator.Todos;

    public ObservableCollection<ChecklistEntry> Checklist { get; }
    public ObservableCollection<ReminderEntry> Reminders { get; }

    public EditTodoVm(TodoItem item)
    {
        _item = item;
        Title = item.Title;
        Notes = item.Note;
        Difficulty = item.Difficulty;
        Priority = item.Priority;
        HasDue = item.Due.HasValue;
        if (item.Due.HasValue)
        {
            var local = item.Due.Value;
            if (local.Kind == DateTimeKind.Utc) local = local.ToLocalTime();
            DueDate = local.Date;
            DueTime = local.TimeOfDay;
        }
        TagsText = string.Join(", ", item.Tags ?? new ObservableCollection<string>());

        Checklist = item.Checklist ?? new ObservableCollection<ChecklistEntry>();
        Reminders = item.Reminders ?? new ObservableCollection<ReminderEntry>();
    }

    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public IReadOnlyList<TodoDifficulty> DifficultyOptions { get; } = new[]
    {
        TodoDifficulty.VeryEasy,
        TodoDifficulty.Easy,
        TodoDifficulty.Medium,
        TodoDifficulty.Hard,
        TodoDifficulty.VeryHard
    };
    public TodoDifficulty Difficulty { get; set; } = TodoDifficulty.Medium;

    public IReadOnlyList<TodoPriority> PriorityOptions { get; } = new[]
    {
        TodoPriority.Low,
        TodoPriority.Medium,
        TodoPriority.High
    };
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    public bool HasDue { get; set; }
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);
    public TimeSpan DueTime { get; set; } = new TimeSpan(9,0,0);

    public string TagsText { get; set; } = string.Empty;

    // Checklist helpers
    public void AddChecklistEntry(string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0) return;
        Checklist.Add(new ChecklistEntry { Text = text, IsDone = false });
        OnPropertyChanged(nameof(Checklist));
    }

    public void RemoveChecklistEntry(ChecklistEntry entry)
    {
        if (entry != null && Checklist.Remove(entry)) OnPropertyChanged(nameof(Checklist));
    }

    // Reminder helpers
    public void AddReminderEntry(string? title, DateTime when)
    {
        var entry = new ReminderEntry { Title = string.IsNullOrWhiteSpace(title) ? null : title!.Trim(), When = when.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(when, DateTimeKind.Local) : when };
        Reminders.Add(entry);
        OnPropertyChanged(nameof(Reminders));
    }

    public void RemoveReminderEntry(ReminderEntry entry)
    {
        if (entry != null && Reminders.Remove(entry)) OnPropertyChanged(nameof(Reminders));
    }

    public async Task<bool> SaveAsync()
    {
        var t = (Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t)) return false;

        DateTime? due = null;
        if (HasDue)
        {
            var local = new DateTime(DueDate.Year, DueDate.Month, DueDate.Day, DueTime.Hours, DueTime.Minutes, 0, DateTimeKind.Local);
            due = local;
        }

        var tags = ParseTags(TagsText).ToList();

        // Update item
        _item.Title = t;
        _item.Note = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim();
        _item.Difficulty = Difficulty;
        _item.Priority = Priority;
        _item.Due = due;
        _item.Tags = new ObservableCollection<string>(tags);
        _item.UpdatedAt = DateTime.UtcNow;

        await _service.SaveAsync();
        await _service.RescheduleNotificationsAsync(_item);
        _service.NotifyChanged();
        return true;
    }

    static IEnumerable<string> ParseTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var parts = raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length == 0) continue;
            if (seen.Add(t)) yield return t;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
