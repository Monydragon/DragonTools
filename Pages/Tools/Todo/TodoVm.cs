using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using Microsoft.Maui.Controls;
using DragonTools.Models;
using DragonTools.Services;

namespace DragonTools.Pages.Tools.Todo;

public enum TodoSortBy { DueDate, Priority, Difficulty, CreatedAt, Title, Completed }
public enum GroupByOption { None, DueDate, Priority }

public sealed class TodoVm : INotifyPropertyChanged
{
    private static readonly TodoService _service = ServiceLocator.Todos;

    public ObservableCollection<TodoItem> DisplayItems { get; } = new();
    public ObservableCollection<string> AvailableTags { get; } = new();
    public ObservableCollection<Group> GroupedItems { get; } = new();

    public ICommand ClearNewCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ToggleCompleteCommand { get; }
    public ICommand SelectTagCommand { get; }

    public IReadOnlyList<TodoPriority> PriorityOptions { get; } = new[]
    {
        TodoPriority.Low,
        TodoPriority.Medium,
        TodoPriority.High
    };

    public IReadOnlyList<TodoSortBy> SortOptions { get; } = new[]
    {
        TodoSortBy.DueDate,
        TodoSortBy.Priority,
        TodoSortBy.Difficulty,
        TodoSortBy.CreatedAt,
        TodoSortBy.Title,
        TodoSortBy.Completed
    };

    public IReadOnlyList<GroupByOption> GroupOptions { get; } = new[] { GroupByOption.None, GroupByOption.DueDate, GroupByOption.Priority };

    private TodoSortBy _sortBy = TodoSortBy.DueDate;
    public TodoSortBy SortBy
    {
        get => _sortBy;
        set { if (_sortBy != value) { _sortBy = value; OnPropertyChanged(); SaveViewPrefs(); Recompute(); } }
    }

    private bool _sortAsc = true;
    public bool SortAscending
    {
        get => _sortAsc;
        set { if (_sortAsc != value) { _sortAsc = value; OnPropertyChanged(); SaveViewPrefs(); Recompute(); } }
    }

    private string _search = string.Empty;
    public string Search
    {
        get => _search;
        set { if (_search != value) { _search = value; OnPropertyChanged(); Recompute(); } }
    }

    private bool _hideCompleted;
    public bool HideCompleted
    {
        get => _hideCompleted;
        set { if (_hideCompleted != value) { _hideCompleted = value; OnPropertyChanged(); SaveViewPrefs(); Recompute(); } }
    }

    private string? _selectedTag;
    public string? SelectedTag
    {
        get => _selectedTag;
        set { if (_selectedTag != value) { _selectedTag = value; OnPropertyChanged(); SaveViewPrefs(); Recompute(); } }
    }

    private GroupByOption _groupBy = GroupByOption.None;
    public GroupByOption GroupBy
    {
        get => _groupBy;
        set { if (_groupBy != value) { _groupBy = value; OnPropertyChanged(); SaveViewPrefs(); Recompute(); } }
    }

    public TodoVm()
    {
        ClearNewCommand = new Command(ClearNew);
        DeleteCommand = new Command<TodoItem>(async (item) => { if (item != null) await _service.RemoveAndSaveAsync(item); });
        ToggleCompleteCommand = new Command<TodoItem>(async (item) => { if (item != null) await _service.SetCompleteAndSaveAsync(item, !item.IsCompleted); });
        SelectTagCommand = new Command<string?>(tag => SelectedTag = string.IsNullOrWhiteSpace(tag) ? null : tag);
        _service.Changed += (_, __) => Recompute();

        LoadViewPrefs();
        Recompute();
    }

    void LoadViewPrefs()
    {
        var s = SettingsService.GetTodoView();
        _sortBy = s.SortBy;
        _sortAsc = s.SortAscending;
        _hideCompleted = s.HideCompleted;
        _groupBy = s.GroupBy;
        _selectedTag = s.SelectedTag;
        OnPropertyChanged(nameof(SortBy));
        OnPropertyChanged(nameof(SortAscending));
        OnPropertyChanged(nameof(HideCompleted));
        OnPropertyChanged(nameof(GroupBy));
        OnPropertyChanged(nameof(SelectedTag));
    }

    void SaveViewPrefs()
    {
        SettingsService.SetTodoView(new TodoViewSettings
        {
            SortBy = _sortBy,
            SortAscending = _sortAsc,
            HideCompleted = _hideCompleted,
            GroupBy = _groupBy,
            SelectedTag = _selectedTag
        });
    }

    void Recompute()
    {
        var all = FlattenVisible(_service.Roots);

        // Compute AvailableTags from all items (pre-filter)
        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in all)
            foreach (var t in item.Tags)
                if (!string.IsNullOrWhiteSpace(t)) tagSet.Add(t.Trim());
        // apply to collection
        var newTags = tagSet.OrderBy(s => s).ToList();
        if (!AvailableTags.SequenceEqual(newTags))
        {
            AvailableTags.Clear();
            foreach (var t in newTags) AvailableTags.Add(t);
            OnPropertyChanged(nameof(AvailableTags));
        }

        // After computing AvailableTags, ensure SelectedTag still exists
        if (!string.IsNullOrWhiteSpace(SelectedTag) && !AvailableTags.Contains(SelectedTag))
        {
            _selectedTag = null; OnPropertyChanged(nameof(SelectedTag)); SaveViewPrefs();
        }

        // Filter
        IEnumerable<TodoItem> filtered = all;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            filtered = filtered.Where(i => (!string.IsNullOrEmpty(i.Title) && i.Title.Contains(s, StringComparison.OrdinalIgnoreCase))
                                        || (!string.IsNullOrEmpty(i.Note) && i.Note.Contains(s, StringComparison.OrdinalIgnoreCase))
                                        || (i.Tags.Count > 0 && string.Join(',', i.Tags).Contains(s, StringComparison.OrdinalIgnoreCase)));
        }
        if (HideCompleted)
        {
            filtered = filtered.Where(i => !i.IsCompleted);
        }
        if (!string.IsNullOrWhiteSpace(SelectedTag))
        {
            var tag = SelectedTag!;
            filtered = filtered.Where(i => i.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        }

        // Sort
        IEnumerable<TodoItem> sorted = (SortBy, SortAscending) switch
        {
            (TodoSortBy.DueDate, true) => filtered.OrderBy(i => i.Due ?? DateTime.MaxValue),
            (TodoSortBy.DueDate, false) => filtered.OrderByDescending(i => i.Due ?? DateTime.MinValue),
            (TodoSortBy.Priority, true) => filtered.OrderBy(i => i.Priority),
            (TodoSortBy.Priority, false) => filtered.OrderByDescending(i => i.Priority),
            (TodoSortBy.Difficulty, true) => filtered.OrderBy(i => i.Difficulty),
            (TodoSortBy.Difficulty, false) => filtered.OrderByDescending(i => i.Difficulty),
            (TodoSortBy.CreatedAt, true) => filtered.OrderBy(i => i.CreatedAt),
            (TodoSortBy.CreatedAt, false) => filtered.OrderByDescending(i => i.CreatedAt),
            (TodoSortBy.Title, true) => filtered.OrderBy(i => i.Title),
            (TodoSortBy.Title, false) => filtered.OrderByDescending(i => i.Title),
            (TodoSortBy.Completed, true) => filtered.OrderBy(i => i.IsCompleted),
            (TodoSortBy.Completed, false) => filtered.OrderByDescending(i => i.IsCompleted),
            _ => filtered
        };

        var finalList = sorted.ToList();

        // Update flat list
        DisplayItems.Clear();
        foreach (var item in finalList) DisplayItems.Add(item);
        OnPropertyChanged(nameof(DisplayItems));

        // Build groups
        var groups = GroupBy switch
        {
            GroupByOption.DueDate => BuildDueGroups(finalList),
            GroupByOption.Priority => BuildPriorityGroups(finalList),
            _ => new List<Group> { new Group("All", finalList) }
        };
        GroupedItems.Clear();
        foreach (var g in groups) GroupedItems.Add(g);
        OnPropertyChanged(nameof(GroupedItems));
    }

    List<Group> BuildDueGroups(List<TodoItem> items)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var endOfWeek = today.AddDays(7 - (int)today.DayOfWeek);

        var overdue = items.Where(i => i.Due.HasValue && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value) < now).ToList();
        var todayList = items.Where(i => i.Due.HasValue && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value).Date == today).ToList();
        var tomorrowList = items.Where(i => i.Due.HasValue && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value).Date == today.AddDays(1)).ToList();
        var thisWeek = items.Where(i => i.Due.HasValue && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value).Date > today.AddDays(1) && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value).Date <= endOfWeek).ToList();
        var later = items.Where(i => i.Due.HasValue && (i.Due!.Value.Kind == DateTimeKind.Utc ? i.Due!.Value.ToLocalTime() : i.Due!.Value).Date > endOfWeek).ToList();
        var noDue = items.Where(i => !i.Due.HasValue).ToList();

        var result = new List<Group>();
        if (overdue.Count > 0) result.Add(new Group("Overdue", overdue));
        if (todayList.Count > 0) result.Add(new Group("Today", todayList));
        if (tomorrowList.Count > 0) result.Add(new Group("Tomorrow", tomorrowList));
        if (thisWeek.Count > 0) result.Add(new Group("This Week", thisWeek));
        if (later.Count > 0) result.Add(new Group("Later", later));
        if (noDue.Count > 0) result.Add(new Group("No Due", noDue));
        return result;
    }

    List<Group> BuildPriorityGroups(List<TodoItem> items)
    {
        return new List<Group>
        {
            new Group("High", items.Where(i => i.Priority == TodoPriority.High)),
            new Group("Medium", items.Where(i => i.Priority == TodoPriority.Medium)),
            new Group("Low", items.Where(i => i.Priority == TodoPriority.Low))
        }.Where(g => g.Count > 0).ToList();
    }

    static IEnumerable<TodoItem> FlattenVisible(IEnumerable<TodoItem> roots)
    {
        foreach (var r in roots)
        {
            yield return r;
            if (r.IsExpanded)
            {
                foreach (var c in FlattenVisible(r.SubTasks))
                    yield return c;
            }
        }
    }

    public void Delete(TodoItem item)
    {
        if (item == null) return;
        _ = _service.RemoveAndSaveAsync(item);
    }

    void ClearNew()
    {
        Search = string.Empty;
        SortBy = TodoSortBy.DueDate;
        SortAscending = true;
        HideCompleted = false;
        SelectedTag = null;
        GroupBy = GroupByOption.None;
    }

    public void Refresh() => Recompute();

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class Group : ObservableCollection<TodoItem>
    {
        public string Title { get; }
        public Group(string title, IEnumerable<TodoItem> items) : base(items)
        {
            Title = title;
        }
    }
}
