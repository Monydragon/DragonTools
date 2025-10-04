using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DragonTools.Models;
using DragonTools.Services;

namespace DragonTools.Pages.Tools.Todo;

public sealed class CreateTodoVm : INotifyPropertyChanged
{
    private readonly TodoService _service = ServiceLocator.Todos;
    private readonly TodoItem? _parent;
    public TodoItem? Parent => _parent; // expose for deferred add

    public CreateTodoVm(TodoItem? parent = null)
    {
        _parent = parent;
        if (_parent != null) ParentTitle = _parent.Title;
        _removeTagCommand = new Command<string>(RemoveTag);
    }

    public string? ParentTitle { get; }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    private string? _notes;
    public string? Notes
    {
        get => _notes;
        set { if (_notes != value) { _notes = value; OnPropertyChanged(); } }
    }

    public IReadOnlyList<TodoDifficulty> DifficultyOptions { get; } = new[]
    {
        TodoDifficulty.VeryEasy,
        TodoDifficulty.Easy,
        TodoDifficulty.Medium,
        TodoDifficulty.Hard,
        TodoDifficulty.VeryHard
    };

    private TodoDifficulty _difficulty = TodoDifficulty.Medium;
    public TodoDifficulty Difficulty
    {
        get => _difficulty;
        set { if (_difficulty != value) { _difficulty = value; OnPropertyChanged(); } }
    }

    private bool _hasDue;
    public bool HasDue
    {
        get => _hasDue;
        set { if (_hasDue != value) { _hasDue = value; OnPropertyChanged(); } }
    }

    private DateTime _dueDate = DateTime.Today.AddDays(1);
    public DateTime DueDate
    {
        get => _dueDate;
        set { if (_dueDate != value) { _dueDate = value; OnPropertyChanged(); } }
    }

    private TimeSpan _dueTime = new(9, 0, 0);
    public TimeSpan DueTime
    {
        get => _dueTime;
        set { if (_dueTime != value) { _dueTime = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<ChecklistEntry> Checklist { get; } = new();
    public ObservableCollection<ReminderEntry> Reminders { get; } = new();

    public DateTime NewReminderDate { get; set; } = DateTime.Today.AddDays(1);
    public TimeSpan NewReminderTime { get; set; } = new(9,0,0);

    public IEnumerable<string> TagChips => ParseTags(TagsText);

    public ICommand RemoveTagCommand => _removeTagCommand;
    private readonly ICommand _removeTagCommand;

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

    public void AddReminder()
    {
        var local = new DateTime(NewReminderDate.Year, NewReminderDate.Month, NewReminderDate.Day, NewReminderTime.Hours, NewReminderTime.Minutes, 0, DateTimeKind.Local);
        Reminders.Add(new ReminderEntry { When = local });
        OnPropertyChanged(nameof(Reminders));
    }

    public void RemoveReminder(ReminderEntry entry)
    {
        if (Reminders.Remove(entry)) OnPropertyChanged(nameof(Reminders));
    }

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

    // Ensure TagChips updates when TagsText changes
    private string _tagsText = string.Empty;
    public string TagsText
    {
        get => _tagsText;
        set { if (_tagsText != value) { _tagsText = value; OnPropertyChanged(); OnPropertyChanged(nameof(TagChips)); } }
    }

    private bool _isCompleted = false;
    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted != value) { _isCompleted = value; OnPropertyChanged(); } }
    }

    public async Task<bool> SaveAsync()
    {
        var t = Title.Trim();
        if (string.IsNullOrEmpty(t)) return false;

        DateTime? due = null;
        if (HasDue)
        {
            var local = new DateTime(DueDate.Year, DueDate.Month, DueDate.Day, DueTime.Hours, DueTime.Minutes, 0, DateTimeKind.Local);
            due = local;
        }

        var tags = ParseTags(TagsText).ToList();

        var item = new TodoItem
        {
            Title = t,
            Note = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim(),
            Difficulty = Difficulty,
            Due = due,
            Tags = new ObservableCollection<string>(tags),
            Checklist = new ObservableCollection<ChecklistEntry>(Checklist),
            Reminders = new ObservableCollection<ReminderEntry>(Reminders),
            IsCompleted = IsCompleted, // Use the property value
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _service.AddAndSaveAsync(item, _parent);
        return true;
    }

    public bool TryBuildItem(out TodoItem? item)
    {
        item = null;
        var t = Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(t)) return false;

        DateTime? due = null;
        if (HasDue)
        {
            var local = new DateTime(DueDate.Year, DueDate.Month, DueDate.Day, DueTime.Hours, DueTime.Minutes, 0, DateTimeKind.Local);
            due = local;
        }
        var tags = ParseTags(TagsText).ToList();
        item = new TodoItem
        {
            Title = t,
            Note = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim(),
            Difficulty = Difficulty,
            Due = due,
            Tags = new ObservableCollection<string>(tags),
            Checklist = new ObservableCollection<ChecklistEntry>(Checklist),
            Reminders = new ObservableCollection<ReminderEntry>(Reminders),
            IsCompleted = IsCompleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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

    public void RemoveTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        var remaining = ParseTags(TagsText).Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToList();
        TagsText = string.Join(", ", remaining);
        OnPropertyChanged(nameof(TagChips));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
