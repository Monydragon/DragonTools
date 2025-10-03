using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // MainThread
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
    public ICommand ToggleFiltersCommand { get; }
    public ICommand AddQuickCommand => _addQuickCommand; // expose
    Command _addQuickCommand; // backing

    // Quick add title
    string _newTitle = string.Empty;
    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (_newTitle != value)
            {
                _newTitle = value;
                OnPropertyChanged();
                _addQuickCommand.ChangeCanExecute();
            }
        }
    }

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

    private bool _areFiltersVisible;
    public bool AreFiltersVisible
    {
        get => _areFiltersVisible;
        set { if (_areFiltersVisible != value) { _areFiltersVisible = value; OnPropertyChanged(); } }
    }

    private bool _singleExpand = true;
    public bool SingleExpand
    {
        get => _singleExpand;
        set { if (_singleExpand != value) { _singleExpand = value; OnPropertyChanged(); } }
    }

    public void ExpandExclusive(TodoItem item)
    {
        if (!SingleExpand) return;
        // Collapse every other expanded item
        foreach (var other in DisplayItems)
        {
            if (!ReferenceEquals(other, item) && other.IsExpanded)
                other.IsExpanded = false;
        }
    }

    public TodoVm()
    {
        ClearNewCommand = new Command(ClearNew);
        DeleteCommand = new Command<TodoItem>(async (item) => { if (item != null) await _service.RemoveAndSaveAsync(item); });
        ToggleCompleteCommand = new Command<TodoItem>(async (item) => { if (item != null) await _service.SetCompleteAndSaveAsync(item, !item.IsCompleted); });
        SelectTagCommand = new Command<string?>(tag => SelectedTag = string.IsNullOrWhiteSpace(tag) ? null : tag);
        ToggleFiltersCommand = new Command(() => AreFiltersVisible = !AreFiltersVisible);
        _addQuickCommand = new Command(async () => await AddQuickAsync(), () => !string.IsNullOrWhiteSpace(NewTitle));
        // Marshal service change events to UI thread to avoid WinUI crashes when updating ObservableCollections
        _service.Changed += OnServiceChanged;

        LoadViewPrefs();
        Recompute();
    }

    async Task AddQuickAsync()
    {
        var title = NewTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return;
        var item = new TodoItem { Title = title }; // defaults already set in model
        await _service.AddAndSaveAsync(item);
        NewTitle = string.Empty; // clears & triggers CanExecute update
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        if (MainThread.IsMainThread)
            Recompute();
        else
            MainThread.BeginInvokeOnMainThread(Recompute);
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

    // Re-entrancy guard to prevent WinUI collection churn crashes
    bool _recomputing;
    bool _recomputePending;

    List<TodoItem>? _pendingDisplay;
    bool _displayUpdateScheduled;

    void Recompute()
    {
        if (_recomputing)
        {
            _recomputePending = true;
            return;
        }
        try
        {
            _recomputing = true;
            var rootSnapshot = _service.Roots.ToList();
            IEnumerable<TodoItem> all;
            try
            {
                all = FlattenVisibleSnapshot(rootSnapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TodoVm] Flatten failed: {ex}");
                all = Enumerable.Empty<TodoItem>();
            }
            try
            {
                var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in all)
                    foreach (var t in item.Tags)
                        if (!string.IsNullOrWhiteSpace(t)) tagSet.Add(t.Trim());
                var newTags = tagSet.OrderBy(s => s).ToList();
                if (!AvailableTags.SequenceEqual(newTags))
                {
                    AvailableTags.Clear();
                    foreach (var t in newTags) AvailableTags.Add(t);
                    OnPropertyChanged(nameof(AvailableTags));
                }
                if (!string.IsNullOrWhiteSpace(SelectedTag) && !AvailableTags.Contains(SelectedTag))
                {
                    _selectedTag = null; OnPropertyChanged(nameof(SelectedTag)); SaveViewPrefs();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TodoVm] Tag computation failed: {ex}"); }

            List<TodoItem> finalList;
            try
            {
                IEnumerable<TodoItem> filtered = all;
                if (!string.IsNullOrWhiteSpace(Search))
                {
                    var s = Search.Trim();
                    filtered = filtered.Where(i => (!string.IsNullOrEmpty(i.Title) && i.Title.Contains(s, StringComparison.OrdinalIgnoreCase))
                                                || (!string.IsNullOrEmpty(i.Note) && i.Note.Contains(s, StringComparison.OrdinalIgnoreCase))
                                                || (i.Tags.Count > 0 && string.Join(',', i.Tags).Contains(s, StringComparison.OrdinalIgnoreCase)));
                }
                if (HideCompleted) filtered = filtered.Where(i => !i.IsCompleted);
                if (!string.IsNullOrWhiteSpace(SelectedTag))
                {
                    var tag = SelectedTag!;
                    filtered = filtered.Where(i => i.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
                }
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
                finalList = sorted.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TodoVm] Filter/sort failed: {ex}");
                finalList = new List<TodoItem>();
            }

            // Queue display update (avoid modifying bound collection during layout measure)
            _pendingDisplay = finalList;
            if (!_displayUpdateScheduled)
            {
                _displayUpdateScheduled = true;
                MainThread.BeginInvokeOnMainThread(ApplyPendingDisplay);
            }

            // Groups can also wait until after display applied; but building them here snapshot is fine.
            try
            {
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TodoVm] Group build failed: {ex}"); }
        }
        finally
        {
            _recomputing = false;
            if (_recomputePending)
            {
                _recomputePending = false;
                MainThread.BeginInvokeOnMainThread(Recompute);
            }
        }
    }

    void ApplyPendingDisplay()
    {
        _displayUpdateScheduled = false;
        if (_pendingDisplay == null) return;
        try
        {
            var list = _pendingDisplay;
            _pendingDisplay = null;
            bool changed = DisplayItems.Count != list.Count;
            if (!changed)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!ReferenceEquals(DisplayItems[i], list[i])) { changed = true; break; }
                }
            }
            if (changed)
            {
                DisplayItems.Clear();
                foreach (var item in list) DisplayItems.Add(item);
                OnPropertyChanged(nameof(DisplayItems));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TodoVm] ApplyPendingDisplay failed: {ex}");
        }
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

    // Snapshot-based non-recursive flatten used in Recompute to avoid collection modification during enumeration
    static IEnumerable<TodoItem> FlattenVisibleSnapshot(IList<TodoItem> roots)
    {
        var stack = new Stack<TodoItem>(roots.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            if (current.IsExpanded && current.SubTasks.Count > 0)
            {
                var children = current.SubTasks.ToList();
                for (int i = children.Count - 1; i >= 0; i--)
                    stack.Push(children[i]);
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
        public string Title { get; set; }
        // You can add other properties if needed, such as Key, Color, etc.
        public Group(string title, IEnumerable<TodoItem> items) : base(items)
        {
            Title = title;
        }
        public Group(string title) : base()
        {
            Title = title;
        }
    }
}
