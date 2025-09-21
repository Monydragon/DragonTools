using System.ComponentModel;
using System.Runtime.CompilerServices;
using DragonTools.Models;
using DragonTools.Services;

namespace DragonTools.Pages.Tools.Todo;

public sealed class EditTodoVm : INotifyPropertyChanged
{
    private readonly TodoItem _item;
    private readonly TodoService _service = ServiceLocator.Todos;

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
        TagsText = string.Join(", ", item.Tags ?? new List<string>());
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

        var tags = (TagsText ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Update item
        _item.Title = t;
        _item.Note = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim();
        _item.Difficulty = Difficulty;
        _item.Priority = Priority;
        _item.Due = due;
        _item.Tags = tags;
        _item.UpdatedAt = DateTime.UtcNow;

        await _service.SaveAsync();
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
