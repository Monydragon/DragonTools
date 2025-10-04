using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Linq; // Added for RightInfo LINQ Count

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
        set { if (_due != value) { _due = value; Touch(); OnPropertyChanged(); OnPropertyChanged(nameof(IsOverdue)); OnPropertyChanged(nameof(IsDueSoon)); OnPropertyChanged(nameof(TileColor)); OnPropertyChanged(nameof(RightInfo)); } }
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
        set { if (_isCompleted != value) { _isCompleted = value; if (value) { _isExpanded = false; OnPropertyChanged(nameof(IsExpanded)); } Touch(); OnPropertyChanged(); OnPropertyChanged(nameof(IsOverdue)); OnPropertyChanged(nameof(IsDueSoon)); OnPropertyChanged(nameof(TileColor)); OnPropertyChanged(nameof(RightInfo)); } }
    }

    [JsonIgnore]
    public bool IsOverdue
    {
        get
        {
            if (_isCompleted) return false;
            if (!_due.HasValue) return false;
            var d = _due.Value;
            if (d.Kind == DateTimeKind.Utc) d = d.ToLocalTime();
            return d < DateTime.Now;
        }
    }

    [JsonIgnore]
    public bool IsDueSoon
    {
        get
        {
            if (_isCompleted) return false;
            if (!_due.HasValue) return false;
            var d = _due.Value;
            if (d.Kind == DateTimeKind.Utc) d = d.ToLocalTime();
            var now = DateTime.Now;
            if (d < now) return false; // overdue handled separately
            return (d - now) <= TimeSpan.FromHours(24);
        }
    }

    int _depth = 1;
    public int Depth
    {
        get => _depth;
        set { if (_depth != value) { _depth = Math.Max(1, Math.Min(3, value)); OnPropertyChanged(); OnPropertyChanged(nameof(TileColor)); } }
    }

    [JsonIgnore]
    public string TileColor
    {
        get
        {
            if (IsCompleted) return "#059669"; // green
            if (IsOverdue) return "#DC2626";   // red
            if (IsDueSoon) return "#F59E0B";   // amber
            return "#60C4FF"; // uniform light blue to match design screenshot
        }
    }

    ObservableCollection<TodoItem> _subTasks = new();
    public ObservableCollection<TodoItem> SubTasks
    {
        get => _subTasks;
        set {
            if (_subTasks != value) {
                if (_subTasks != null) _subTasks.CollectionChanged -= SubTasks_CollectionChanged;
                _subTasks = value; 
                if (_subTasks != null) _subTasks.CollectionChanged += SubTasks_CollectionChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RightInfo));
            }
        }
    }
    void SubTasks_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(RightInfo));

    ObservableCollection<string> _tags = new();
    public ObservableCollection<string> Tags
    {
        get => _tags;
        set {
            if (!ReferenceEquals(_tags, value)) {
                if (_tags != null) _tags.CollectionChanged -= Tags_CollectionChanged;
                _tags = value ?? new ObservableCollection<string>();
                _tags.CollectionChanged += Tags_CollectionChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LimitedTags));
                OnPropertyChanged(nameof(TagsOverflow));
                OnPropertyChanged(nameof(HasTagsOverflow));
            }
        }
    }
    void Tags_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LimitedTags));
        OnPropertyChanged(nameof(TagsOverflow));
        OnPropertyChanged(nameof(HasTagsOverflow));
    }

    public ObservableCollection<ChecklistEntry> Checklist { get; set; } = new();
    public ObservableCollection<ReminderEntry> Reminders { get; set; } = new();

    public bool CanAddSubtask => Depth < 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    void Touch() => UpdatedAt = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool _isExpanded = false; // default collapsed
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }

    [JsonIgnore]
    public string RightInfo
    {
        get
        {
            // Show remaining checklist items if any, else subtask count, else blank
            int remainingChecklist = Checklist.Count(c => !c.IsDone);
            if (remainingChecklist > 0) return remainingChecklist.ToString();
            if (SubTasks.Count > 0) return SubTasks.Count.ToString();
            return string.Empty;
        }
    }

    [JsonIgnore]
    public int RemainingChecklist => Checklist.Count(c => !c.IsDone);

    [JsonIgnore]
    public string DueShort
    {
        get
        {
            if (!Due.HasValue) return string.Empty;
            var d = Due.Value.Kind == DateTimeKind.Utc ? Due.Value.ToLocalTime() : Due.Value;
            var today = DateTime.Now.Date;
            if (d.Date == today) return d.ToString("HH:mm");
            if (d.Date == today.AddDays(1)) return "tomorrow";
            return d.ToString("MMM d");
        }
    }

    [JsonIgnore]
    public IEnumerable<string> LimitedTags => Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Take(5);

    [JsonIgnore]
    public int TagsOverflow => Math.Max(0, Tags.Count(t => !string.IsNullOrWhiteSpace(t)) - 5);
    [JsonIgnore]
    public bool HasTagsOverflow => TagsOverflow > 0;

    [JsonIgnore]
    bool _isSelected;
    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public TodoItem()
    {
        // Hook collection changes to update RightInfo automatically
        Checklist.CollectionChanged += (_, __) => { OnPropertyChanged(nameof(RightInfo)); OnPropertyChanged(nameof(RemainingChecklist)); };
        SubTasks.CollectionChanged += (_, __) => { OnPropertyChanged(nameof(RightInfo)); };
        _tags.CollectionChanged += Tags_CollectionChanged;
    }
}
