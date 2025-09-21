using System;
using Microsoft.Maui.Storage;
using DragonTools.Pages.Tools.Todo; // for enums

namespace DragonTools.Services;

public enum AppThemePref { System = 0, Light = 1, Dark = 2 }
public enum BulkPlaceholderStyle { SingleLine = 0, MultiLine = 1 }

public sealed class RandomPickerSettings
{
    public bool Deduplicate { get; set; } = true;
    public bool NormalizeSpaces { get; set; } = true;
    public BulkPlaceholderStyle PlaceholderStyle { get; set; } = BulkPlaceholderStyle.SingleLine;
}

public sealed class TodoViewSettings
{
    public TodoSortBy SortBy { get; set; } = TodoSortBy.DueDate;
    public bool SortAscending { get; set; } = true;
    public bool HideCompleted { get; set; } = false;
    public GroupByOption GroupBy { get; set; } = GroupByOption.None;
    public string? SelectedTag { get; set; }
}

public static class SettingsService
{
    // --------- Keys
    const string ThemeKey = "app.theme";
    const string RP_DedupKey = "rp.dedup";
    const string RP_NormKey = "rp.norm";
    const string RP_PlaceholderKey = "rp.placeholder";
    const string TV_Sort = "todo.view.sort";
    const string TV_SortAsc = "todo.view.sortAsc";
    const string TV_Hide = "todo.view.hideCompleted";
    const string TV_Group = "todo.view.group";
    const string TV_Tag = "todo.view.selectedTag";

    // --------- Global theme
    public static AppThemePref GetTheme()
    {
        var v = Preferences.Get(ThemeKey, (int)AppThemePref.System);
        return Enum.IsDefined(typeof(AppThemePref), v) ? (AppThemePref)v : AppThemePref.System;
    }

    public static void SetTheme(AppThemePref pref)
    {
        Preferences.Set(ThemeKey, (int)pref);
        var actual = pref switch
        {
            AppThemePref.Light => AppTheme.Light,
            AppThemePref.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
        Application.Current!.UserAppTheme = actual;
    }

    // --------- Random Picker
    public static RandomPickerSettings GetRandomPicker()
    {
        return new RandomPickerSettings
        {
            Deduplicate = Preferences.Get(RP_DedupKey, true),
            NormalizeSpaces = Preferences.Get(RP_NormKey, true),
            PlaceholderStyle = (BulkPlaceholderStyle)Preferences.Get(RP_PlaceholderKey, (int)BulkPlaceholderStyle.SingleLine),
        };
    }

    public static void SetRandomPicker(RandomPickerSettings s)
    {
        Preferences.Set(RP_DedupKey, s.Deduplicate);
        Preferences.Set(RP_NormKey, s.NormalizeSpaces);
        Preferences.Set(RP_PlaceholderKey, (int)s.PlaceholderStyle);
        RandomPickerChanged?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? RandomPickerChanged;

    // --------- Todo View
    public static TodoViewSettings GetTodoView()
    {
        return new TodoViewSettings
        {
            SortBy = (TodoSortBy)Preferences.Get(TV_Sort, (int)TodoSortBy.DueDate),
            SortAscending = Preferences.Get(TV_SortAsc, true),
            HideCompleted = Preferences.Get(TV_Hide, false),
            GroupBy = (GroupByOption)Preferences.Get(TV_Group, (int)GroupByOption.None),
            SelectedTag = Preferences.Get(TV_Tag, (string?)null)
        };
    }

    public static void SetTodoView(TodoViewSettings s)
    {
        Preferences.Set(TV_Sort, (int)s.SortBy);
        Preferences.Set(TV_SortAsc, s.SortAscending);
        Preferences.Set(TV_Hide, s.HideCompleted);
        Preferences.Set(TV_Group, (int)s.GroupBy);
        if (s.SelectedTag == null) Preferences.Remove(TV_Tag); else Preferences.Set(TV_Tag, s.SelectedTag);
        TodoViewChanged?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? TodoViewChanged;
}
