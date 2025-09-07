using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using DragonTools.Models; // NormalChoice, WeightedChoice

namespace DragonTools.Pages.Tools.RandomPicker;

public partial class RandomPickerPage : ContentPage
{
    private readonly RandomPickerVm _vm = new();

    public RandomPickerPage()
    {
        InitializeComponent();
        BindingContext = _vm;
        _vm.RefreshSavedLists();
    }

    // ====================== TOP BAR ======================

    private async void CreateList_Clicked(object sender, EventArgs e)
    {
        var type = await DisplayActionSheet("Choose list type", "Cancel", null, "Normal", "Weighted");
        if (type is null or "Cancel") return;

        var name = await DisplayPromptAsync("List name", "Enter a list name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        _vm.CreateNewList(type, name);

        // Save immediately so it shows in dropdown
        await _vm.SaveAsync(allowEmpty: true);
        _vm.RefreshSavedLists();

        var display = ListStorageService.DisplayFor(_vm.ListName, _vm.SelectedListType);

        // case-insensitive lookup so casing differences don't block selection
        var idx = -1;
        for (int i = 0; i < _vm.SavedListDisplay.Count; i++)
        {
            if (string.Equals(_vm.SavedListDisplay[i], display, StringComparison.OrdinalIgnoreCase))
            {
                idx = i; break;
            }
        }
        if (idx >= 0) SavedListPicker.SelectedIndex = idx;
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        var ok = await _vm.SaveAsync();
        if (!ok)
            await DisplayAlert("Save Failed", "Enter a list name and at least one entry.", "OK");
        else
        {
            _vm.RefreshSavedLists();
            await DisplayAlert("Saved", $"Saved '{_vm.ListName}'.", "OK");
        }
    }

    private async void DeleteList_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ListName))
        {
            await DisplayAlert("Delete", "No list selected.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Delete List",
            $"Delete '{_vm.ListName}' ({_vm.SelectedListType})?", "Delete", "Cancel");
        if (!confirm) return;

        var deleted = ListStorageService.Delete(_vm.ListName, _vm.SelectedListType);
        if (!deleted)
        {
            await DisplayAlert("Delete", "List file not found.", "OK");
            return;
        }

        _vm.RefreshSavedLists();
        SavedListPicker.SelectedIndex = -1;
        _vm.ListName = "";
        _vm.Items.Clear();
        _vm.ApplyFilter();
        await DisplayAlert("Deleted", "List removed.", "OK");
    }

    private async void RenameList_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ListName))
        {
            await DisplayAlert("Rename", "No list selected.", "OK");
            return;
        }

        var input = await DisplayPromptAsync("Rename List", "New list name:", initialValue: _vm.ListName);
        if (string.IsNullOrWhiteSpace(input)) return;

        var newName = input.Trim();

        // Only bail if it's exactly the same string (case-sensitive).
        // Case-only changes should proceed.
        if (string.Equals(newName, _vm.ListName, StringComparison.Ordinal))
            return;

        var ok = await _vm.RenameAsync(newName);
        if (!ok)
        {
            await DisplayAlert("Rename", "Could not rename the list (file missing?).", "OK");
            return;
        }

        _vm.RefreshSavedLists();

        // Re-select the renamed item (case-insensitive match)
        var display = ListStorageService.DisplayFor(_vm.ListName, _vm.SelectedListType);
        var idx = -1;
        for (int i = 0; i < _vm.SavedListDisplay.Count; i++)
            if (string.Equals(_vm.SavedListDisplay[i], display, StringComparison.OrdinalIgnoreCase))
            { idx = i; break; }
        if (idx >= 0) SavedListPicker.SelectedIndex = idx;

        await DisplayAlert("Renamed", $"List is now '{_vm.ListName}'.", "OK");
    }


    private async void ChangeType_Clicked(object sender, EventArgs e)
    {
        var pick = await DisplayActionSheet("Change list type", "Cancel", null, "Normal", "Weighted");
        if (pick is null or "Cancel") return;

        var newType = pick.Equals("Weighted", StringComparison.OrdinalIgnoreCase)
            ? ChoiceListType.Weighted : ChoiceListType.Normal;

        var ok = await _vm.ChangeTypeAsync(newType);
        if (!ok)
        {
            await DisplayAlert("Type", "Could not change the type.", "OK");
            return;
        }

        _vm.RefreshSavedLists();
        var display = ListStorageService.DisplayFor(_vm.ListName, _vm.SelectedListType);

// case-insensitive lookup so casing differences don't block selection
        var idx = -1;
        for (int i = 0; i < _vm.SavedListDisplay.Count; i++)
        {
            if (string.Equals(_vm.SavedListDisplay[i], display, StringComparison.OrdinalIgnoreCase))
            {
                idx = i; break;
            }
        }
        if (idx >= 0) SavedListPicker.SelectedIndex = idx;

        await DisplayAlert("Type", $"List type set to '{_vm.SelectedListType}'.", "OK");
    }

    private async void SavedList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (sender is not Picker p || p.SelectedIndex < 0) return;

        var display = _vm.SavedListDisplay[p.SelectedIndex];
        var name = ListStorageService.ParseDisplayName(display, out var type);

        _vm.SelectedListType = type;
        _vm.ListName = name;
        await _vm.LoadAsync(name);
    }

    // ====================== ENTRIES ======================

    private void ToggleEntries_Clicked(object sender, EventArgs e)
        => _vm.EntriesExpanded = !_vm.EntriesExpanded;

    private void OptionsSearch_TextChanged(object sender, TextChangedEventArgs e) => _vm.ApplyFilter();

    private void AddBulkItems_Clicked(object sender, EventArgs e)
    {
        var input = BulkEditor?.Text ?? _vm.BulkText;
        var added = _vm.AddBulkItems(input);

        if (added > 0)
            DisplayAlert("Bulk add", $"Added {added} item(s).", "OK");
        else
            DisplayAlert("Bulk add", "Nothing to add. Check your input.", "OK");
    }

    private async void EditItem_Clicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is ChoiceItem item)
        {
            var text = await DisplayPromptAsync("Edit Entry", "Entry:", initialValue: item.Entry);
            if (string.IsNullOrWhiteSpace(text)) return;

            var newWeight = item.Weight;
            if (_vm.IsWeightedMode)
            {
                var wStr = await DisplayPromptAsync("Edit Weight", "Weight:", initialValue: item.Weight.ToString());
                if (int.TryParse(wStr, out var w) && w > 0) newWeight = w;
            }

            _vm.EditItem(item, text, newWeight);
        }
    }

    private void DeleteItem_Clicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is ChoiceItem item)
            _vm.DeleteItem(item);
    }

    // ====================== ROLL (popup only) ======================

    private async void Roll_Clicked(object sender, EventArgs e)
    {
        var result = await _vm.RollAsync();
        await DisplayAlert("Result", result, "OK");
    }

    // ====================== PAGER ======================

    private void PrevPage_Clicked(object sender, EventArgs e)
    {
        if (_vm.TotalPages == 0) return;
        _vm.SetPage(Math.Max(1, _vm.CurrentPage - 1));
    }

    private void NextPage_Clicked(object sender, EventArgs e)
    {
        if (_vm.TotalPages == 0) return;
        _vm.SetPage(Math.Min(_vm.TotalPages, _vm.CurrentPage + 1));
    }

    // ====================== Settings (gear) ======================
    private async void OpenSettings_Clicked(object sender, EventArgs e)
    {
        // Navigate to your SettingsPage
        await Navigation.PushAsync(new DragonTools.Pages.Settings.SettingsPage());
    }
}

/* ====================== ViewModel & helpers ====================== */

public enum ChoiceListType { Normal, Weighted }

public class ChoiceItem : INotifyPropertyChanged
{
    string _entry = "";
    int _weight = 1;

    public string Entry { get => _entry; set { _entry = value; OnPropertyChanged(); } }
    public int Weight { get => _weight; set { _weight = value < 1 ? 1 : value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));
}

public class RandomPickerVm : INotifyPropertyChanged
{
    public ObservableCollection<ChoiceItem> Items { get; } = new();
    public ObservableCollection<ChoiceItem> FilteredItems { get; } = new();
    public ObservableCollection<ChoiceItem> PagedItems { get; } = new();

    // Persisted identity (for rename/type move)
    string _persistedName = "";
    ChoiceListType _persistedType = ChoiceListType.Normal;

    // Collapsible entries
    bool _entriesExpanded = true;
    public bool EntriesExpanded
    {
        get => _entriesExpanded;
        set
        {
            if (value == _entriesExpanded) return;
            _entriesExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EntriesChevron));
        }
    }
    public string EntriesChevron => EntriesExpanded ? "▾" : "▸";

    // Paging (default page size = 5 as requested)
    int _pageSize = 5;
    public IList<int> PageSizeOptions { get; } = new List<int> { 5, 10, 25, 50, 100 };
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize == value) return;
            _pageSize = value <= 0 ? 5 : value;
            OnPropertyChanged();
            RebuildPage(resetToFirst: true);
        }
    }

    int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (value == _currentPage) return;
            _currentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageLabel));
        }
    }

    public int TotalPages => FilteredItems.Count == 0
        ? 0
        : (int)Math.Ceiling((double)FilteredItems.Count / PageSize);

    public string PageLabel =>
        TotalPages == 0
            ? "0 / 0 • 0 entries"
            : $"{CurrentPage} / {TotalPages} • {FilteredItems.Count} entries";

    // List type & state
    ChoiceListType _selectedListType = ChoiceListType.Normal;
    public ChoiceListType SelectedListType
    {
        get => _selectedListType;
        set
        {
            _selectedListType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWeightedMode));
            OnPropertyChanged(nameof(TypeBadge));
            ApplyFilter();
        }
    }

    public string TypeBadge => $"Type: {SelectedListType}";
    public bool IsWeightedMode => SelectedListType == ChoiceListType.Weighted;

    string _listName = "";
    public string ListName { get => _listName; set { _listName = value; OnPropertyChanged(); } }

    string _searchText = "";
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); } }

    // Bulk add input
    string _bulkText = "";
    public string BulkText { get => _bulkText; set { _bulkText = value; OnPropertyChanged(); } }

    string _lastResult = "—";
    public string LastResult { get => _lastResult; set { _lastResult = value; OnPropertyChanged(); } }

    public ObservableCollection<string> SavedListDisplay { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));

    // -------- Actions --------

    public void CreateNewList(string typeName, string name)
    {
        SelectedListType = typeName.Equals("Weighted", StringComparison.OrdinalIgnoreCase)
            ? ChoiceListType.Weighted : ChoiceListType.Normal;

        ListName = name.Trim();
        Items.Clear();
        FilteredItems.Clear();
        PagedItems.Clear();
        LastResult = "—";
        CurrentPage = 1;

        // Not persisted yet
        _persistedName = "";
        _persistedType = SelectedListType;

        OnPropertyChanged(nameof(PageLabel));
    }

    public int AddBulkItems(string? input)
    {
        var src = input ?? BulkText;
        if (string.IsNullOrWhiteSpace(src)) return 0;

        // Split by comma/newline/semicolon/tab
        var parts = src.Split(new[] { ',', '\n', '\r', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => t.Trim())
                       .Where(t => !string.IsNullOrWhiteSpace(t))
                       .ToList();

        if (parts.Count == 0) return 0;

        // weight formats: :n, *n, x n, (n), [n]
        var re = new Regex(
            @"^\s*(?<entry>.+?)\s*(?:(?::|\*|x)\s*(?<w>\d+)|\(\s*(?<w2>\d+)\s*\)|\[\s*(?<w3>\d+)\s*\])?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        int added = 0;
        foreach (var tok in parts)
        {
            var m = re.Match(tok);
            if (!m.Success) continue;

            var entry = (m.Groups["entry"].Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(entry)) continue;

            var wStr = m.Groups["w"].Success ? m.Groups["w"].Value
                     : m.Groups["w2"].Success ? m.Groups["w2"].Value
                     : m.Groups["w3"].Success ? m.Groups["w3"].Value
                     : null;

            var weight = 1;
            if (IsWeightedMode && !string.IsNullOrWhiteSpace(wStr) && int.TryParse(wStr, out var w) && w > 0)
                weight = w;

            Items.Add(new ChoiceItem { Entry = entry, Weight = weight });
            added++;
        }

        BulkText = "";
        OnPropertyChanged(nameof(BulkText));
        ApplyFilter();

        return added;
    }

    public void EditItem(ChoiceItem item, string newEntry, int newWeight)
    {
        item.Entry = newEntry.Trim();
        item.Weight = newWeight < 1 ? 1 : newWeight;
        ApplyFilter();
    }

    public void DeleteItem(ChoiceItem item)
    {
        Items.Remove(item);
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        var q = (SearchText ?? "").Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? Items.ToList()
            : Items.Where(i => (i.Entry ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredItems.Clear();
        foreach (var i in filtered) FilteredItems.Add(i);

        RebuildPage(resetToFirst: true);
    }

    // Build PagedItems from FilteredItems
    void RebuildPage(bool resetToFirst)
    {
        if (resetToFirst || CurrentPage <= 0) CurrentPage = 1;
        if (TotalPages > 0 && CurrentPage > TotalPages) CurrentPage = TotalPages;

        PagedItems.Clear();
        if (FilteredItems.Count == 0) { OnPropertyChanged(nameof(PageLabel)); return; }

        var slice = FilteredItems
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        foreach (var i in slice) PagedItems.Add(i);
        OnPropertyChanged(nameof(PageLabel));
    }

    public void SetPage(int page)
    {
        if (TotalPages == 0)
        {
            CurrentPage = 1;
            PagedItems.Clear();
            OnPropertyChanged(nameof(PageLabel));
            return;
        }

        page = Math.Clamp(page, 1, TotalPages);
        if (page == CurrentPage && PagedItems.Count > 0) return;
        CurrentPage = page;
        RebuildPage(resetToFirst: false);
    }

    // Returns result string for popup
    public async Task<string> RollAsync()
    {
        if (Items.Count == 0)
        {
            LastResult = "No entries";
            return LastResult;
        }

        string picked;
        if (IsWeightedMode)
        {
            var list = Items.Select(i => new WeightedChoice(i.Entry, i.Weight)).ToList();
            picked = RollWeighted(list);
        }
        else
        {
            var list = Items.Select(i => new NormalChoice(i.Entry)).ToList();
            picked = list[Random.Shared.Next(list.Count)].Name;
        }

        LastResult = picked;
        await Task.CompletedTask;
        return LastResult;
    }

    static string RollWeighted(IList<WeightedChoice> list)
    {
        var total = list.Sum(c => Math.Max(1, c.Weight));
        var r = Random.Shared.Next(1, total + 1);
        var cum = 0;
        foreach (var c in list)
        {
            cum += Math.Max(1, c.Weight);
            if (r <= cum) return c.Name;
        }
        return list.Last().Name;
    }

    public async Task<bool> SaveAsync(bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(ListName)) return false;
        if (!allowEmpty && Items.Count == 0) return false;

        var dto = new ChoiceListDto
        {
            Name = ListName.Trim(),
            Type = SelectedListType.ToString(),
            Items = Items.Select(i => new ChoiceDto { Entry = i.Entry, Weight = i.Weight }).ToList()
        };

        await ListStorageService.SaveAsync(dto);

        // Update persisted identity after successful save
        _persistedName = ListName.Trim();
        _persistedType = SelectedListType;

        RefreshSavedLists();
        return true;
    }

    public async Task<bool> LoadAsync(string name)
    {
        var dto = await ListStorageService.LoadAsync(name.Trim(), SelectedListType);
        if (dto is null) return false;

        ListName = dto.Name ?? "";
        SelectedListType = Enum.TryParse<ChoiceListType>(dto.Type, out var t)
            ? t : ChoiceListType.Normal;

        Items.Clear();
        foreach (var c in dto.Items ?? new List<ChoiceDto>())
        {
            var text = !string.IsNullOrWhiteSpace(c.Entry)
                ? c.Entry!
                : (c.Number.HasValue ? c.Number.Value.ToString() : "");
            var wt = Math.Max(1, c.Weight ?? 1);

            if (!string.IsNullOrWhiteSpace(text))
                Items.Add(new ChoiceItem { Entry = text, Weight = wt });
        }

        ApplyFilter();
        LastResult = "—";

        // Track persisted identity based on what's loaded
        _persistedName = ListName;
        _persistedType = SelectedListType;

        return true;
    }

    public async Task<bool> RenameAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        newName = newName.Trim();

        // Try moving the existing file (if any)
        if (!string.IsNullOrWhiteSpace(_persistedName))
        {
            try { ListStorageService.TryRename(_persistedName, _persistedType, newName, SelectedListType, overwrite: true); }
            catch { /* ignore; we'll save as new below */ }
        }

        ListName = newName;
        var ok = await SaveAsync(allowEmpty: true);
        return ok;
    }

    public async Task<bool> ChangeTypeAsync(ChoiceListType newType)
    {
        if (newType == SelectedListType) return true;

        // Ensure defaults when switching to Weighted
        if (newType == ChoiceListType.Weighted)
            foreach (var it in Items) if (it.Weight < 1) it.Weight = 1;

        // Move file across type if persisted
        if (!string.IsNullOrWhiteSpace(_persistedName))
        {
            try { ListStorageService.TryRename(_persistedName, _persistedType, _persistedName, newType, overwrite: true); }
            catch { /* ignore; we'll save as new below */ }
        }

        SelectedListType = newType;

        var ok = await SaveAsync(allowEmpty: true);
        return ok;
    }

    public void RefreshSavedLists()
    {
        SavedListDisplay.Clear();
        foreach (var s in ListStorageService.ListSavedDisplays()) SavedListDisplay.Add(s);
    }
}

/* ====================== Storage DTOs & Service ====================== */

public class ChoiceListDto
{
    public string? Name { get; set; }
    public string? Type { get; set; } // "Normal" | "Weighted"
    public List<ChoiceDto>? Items { get; set; }
}

// Back-compat DTO: supports Entry (string) and legacy Number (int)
public class ChoiceDto
{
    public string? Entry { get; set; }  // NEW
    public int? Number { get; set; }    // legacy support
    public int? Weight { get; set; }
}

public static class ListStorageService
{
    static readonly string Root = FileSystem.AppDataDirectory;
    static readonly string Folder = System.IO.Path.Combine(Root, "RandomPicker");

    static ListStorageService()
    {
        if (!System.IO.Directory.Exists(Folder))
            System.IO.Directory.CreateDirectory(Folder);
    }

    static string Sanitize(string name)
    {
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name.Trim();
    }

    static string FilePath(string listName, ChoiceListType type)
        => System.IO.Path.Combine(Folder, $"{Sanitize(listName)}.{type.ToString().ToLowerInvariant()}.json");

    public static async Task SaveAsync(ChoiceListDto dto)
    {
        var type = Enum.TryParse<ChoiceListType>(dto.Type ?? "Normal", out var t) ? t : ChoiceListType.Normal;
        var path = FilePath(dto.Name ?? "Unnamed", type);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(path, json);
    }

    public static async Task<ChoiceListDto?> LoadAsync(string name, ChoiceListType type)
    {
        var path = FilePath(name, type);
        if (!System.IO.File.Exists(path))
        {
            var other = type == ChoiceListType.Normal ? ChoiceListType.Weighted : ChoiceListType.Normal;
            var alt = FilePath(name, other);
            if (!System.IO.File.Exists(alt)) return null;
            path = alt;
        }

        var json = await System.IO.File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ChoiceListDto>(json);
    }

    public static string[] ListSavedDisplays()
    {
        if (!System.IO.Directory.Exists(Folder)) return Array.Empty<string>();

        var files = System.IO.Directory.GetFiles(Folder, "*.json");
        var results = new List<string>();

        foreach (var path in files)
        {
            // filename like: "<name>.<type>.json"
            var baseNoJson = System.IO.Path.GetFileNameWithoutExtension(path); // "<name>.<type>"
            var dot = baseNoJson.LastIndexOf('.');
            var fallbackName = dot >= 0 ? baseNoJson[..dot] : baseNoJson;
            var typePart = dot >= 0 ? baseNoJson[(dot + 1)..] : "normal"; // keep lower-case for display

            try
            {
                var json = System.IO.File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<ChoiceListDto>(json);

                // Prefer the Name stored inside the JSON (correct casing), fall back to filename.
                var name = string.IsNullOrWhiteSpace(dto?.Name) ? fallbackName : dto!.Name!.Trim();

                results.Add($"{name} ({typePart})");
            }
            catch
            {
                // If anything goes wrong reading JSON, fall back to filename parts.
                results.Add($"{fallbackName} ({typePart})");
            }
        }

        return results.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }


    public static string DisplayFor(string name, ChoiceListType type)
        => $"{name} ({type.ToString().ToLowerInvariant()})";

    public static string ParseDisplayName(string display, out ChoiceListType type)
    {
        var open = display.LastIndexOf('(');
        var close = display.LastIndexOf(')');
        string n = display;
        type = ChoiceListType.Normal;

        if (open >= 0 && close > open)
        {
            n = display[..open].Trim();
            var t = display.Substring(open + 1, close - open - 1);
            type = t.Equals("weighted", StringComparison.OrdinalIgnoreCase)
                ? ChoiceListType.Weighted : ChoiceListType.Normal;
        }
        return n;
    }

    // NEW: rename/move underlying file (handles type change as well)
    public static bool TryRename(string oldName, ChoiceListType oldType, string newName, ChoiceListType newType, bool overwrite = true)
    {
        var src  = FilePath(oldName, oldType);
        var dest = FilePath(newName, newType);

        if (!System.IO.File.Exists(src)) return false;

        if (string.Equals(src, dest, StringComparison.Ordinal)) return true;

        if (System.IO.File.Exists(dest) &&
            !string.Equals(src, dest, StringComparison.OrdinalIgnoreCase))
        {
            if (!overwrite) return false;
            System.IO.File.Delete(dest);
        }

        // Case-only rename (same path ignoring case, different case)
        if (string.Equals(src, dest, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(src, dest, StringComparison.Ordinal))
        {
            var temp = dest + "." + Guid.NewGuid().ToString("N") + ".tmpcase";
            if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
            System.IO.File.Move(src, temp);
            System.IO.File.Move(temp, dest);
            return true;
        }

        System.IO.File.Move(src, dest);
        return true;
    }



    public static bool Delete(string name, ChoiceListType type)
    {
        var path = FilePath(name, type);
        if (!System.IO.File.Exists(path)) return false;
        System.IO.File.Delete(path);
        return true;
    }
}
