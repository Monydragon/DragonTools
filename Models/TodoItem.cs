using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DragonTools.Models;

public enum TodoPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum TodoDifficulty
{
    VeryEasy = 0,
    Easy = 1,
    Medium = 2,
    Hard = 3,
    VeryHard = 4
}

public sealed class TodoItem : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    string _title = string.Empty;
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; Touch(); OnPropertyChanged(); } }
    }

    string? _note;
    public string? Note
    {
        get => _note;
        set { if (_note != value) { _note = value; Touch(); OnPropertyChanged(); } }
    }

    DateTime? _due;
    public DateTime? Due
    {
        get => _due;
        set { if (_due != value) { _due = value; Touch(); OnPropertyChanged(); } }
    }

    TodoPriority _priority = TodoPriority.Medium;
    public TodoPriority Priority
    {
        get => _priority;
        set { if (_priority != value) { _priority = value; Touch(); OnPropertyChanged(); } }
    }

    TodoDifficulty _difficulty = TodoDifficulty.Medium;
    public TodoDifficulty Difficulty
    {
        get => _difficulty;
        set { if (_difficulty != value) { _difficulty = value; Touch(); OnPropertyChanged(); } }
    }

    bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted != value) { _isCompleted = value; Touch(); OnPropertyChanged(); } }
    }

    public int Depth { get; set; } = 1; // 1 = top-level, max 3

    ObservableCollection<TodoItem> _subTasks = new();
    public ObservableCollection<TodoItem> SubTasks
    {
        get => _subTasks;
        set { if (_subTasks != value) { _subTasks = value; OnPropertyChanged(); } }
    }

    List<string> _tags = new();
    public List<string> Tags
    {
        get => _tags;
        set { if (_tags != value) { _tags = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<DragonTools.Models.ChecklistEntry> Checklist { get; set; } = new();
    public ObservableCollection<DragonTools.Models.ReminderEntry> Reminders { get; set; } = new();

    public bool CanAddSubtask => Depth < 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    void Touch() => UpdatedAt = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }
}
