using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        var idx = _vm.SavedListDisplay.IndexOf(display);
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

    private void OptionsSearch_TextChanged(object sender, TextChangedEventArgs e) => _vm.ApplyFilter();

    private void AddItem_Clicked(object sender, EventArgs e)
    {
        _vm.AddNewItem();
        // Optional: autosave on add
        // _ = _vm.SaveAsync();
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

    string _newItemEntry = "";
    public string NewItemEntry { get => _newItemEntry; set { _newItemEntry = value; OnPropertyChanged(); } }

    string _newItemWeight = "1";
    public string NewItemWeight { get => _newItemWeight; set { _newItemWeight = value; OnPropertyChanged(); } }

    string _lastResult = "—";
    public string LastResult { get => _lastResult; set { _lastResult = value; OnPropertyChanged(); } }

    public ObservableCollection<string> SavedListDisplay { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));

    // Actions

    public void CreateNewList(string typeName, string name)
    {
        SelectedListType = typeName.Equals("Weighted", StringComparison.OrdinalIgnoreCase)
            ? ChoiceListType.Weighted : ChoiceListType.Normal;

        ListName = name.Trim();
        Items.Clear();
        FilteredItems.Clear();
        LastResult = "—";
    }

    public void AddNewItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemEntry)) return;

        var weight = 1;
        if (IsWeightedMode && int.TryParse(NewItemWeight, out var w) && w > 0)
            weight = w;

        Items.Add(new ChoiceItem { Entry = NewItemEntry.Trim(), Weight = weight });

        // reset inputs
        NewItemEntry = "";
        NewItemWeight = "1";
        OnPropertyChanged(nameof(NewItemEntry));
        OnPropertyChanged(nameof(NewItemWeight));

        ApplyFilter();
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
            // Back-compat: accept either Entry (string) or legacy Number (int)
            var text = !string.IsNullOrWhiteSpace(c.Entry)
                ? c.Entry!
                : (c.Number.HasValue ? c.Number.Value.ToString() : "");
            var wt = Math.Max(1, c.Weight ?? 1);

            if (!string.IsNullOrWhiteSpace(text))
                Items.Add(new ChoiceItem { Entry = text, Weight = wt });
        }

        ApplyFilter();
        LastResult = "—";
        return true;
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
    static readonly string Folder = Path.Combine(Root, "RandomPicker");

    static ListStorageService()
    {
        if (!Directory.Exists(Folder))
            Directory.CreateDirectory(Folder);
    }

    static string Sanitize(string name)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name.Trim();
    }

    static string FilePath(string listName, ChoiceListType type)
        => Path.Combine(Folder, $"{Sanitize(listName)}.{type.ToString().ToLowerInvariant()}.json");

    public static async Task SaveAsync(ChoiceListDto dto)
    {
        var type = Enum.TryParse<ChoiceListType>(dto.Type ?? "Normal", out var t) ? t : ChoiceListType.Normal;
        var path = FilePath(dto.Name ?? "Unnamed", type);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<ChoiceListDto?> LoadAsync(string name, ChoiceListType type)
    {
        var path = FilePath(name, type);
        if (!File.Exists(path))
        {
            var other = type == ChoiceListType.Normal ? ChoiceListType.Weighted : ChoiceListType.Normal;
            var alt = FilePath(name, other);
            if (!File.Exists(alt)) return null;
            path = alt;
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ChoiceListDto>(json);
    }

    public static string[] ListSavedDisplays()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<string>();
        var files = Directory.GetFiles(Folder, "*.json");
        return files.Select(f =>
        {
            var baseName = Path.GetFileNameWithoutExtension(f);
            var dot = baseName.LastIndexOf('.');
            if (dot < 0) return baseName;
            var name = baseName[..dot];
            var type = baseName[(dot + 1)..];
            return $"{name} ({type})";
        }).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
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

    public static bool Delete(string name, ChoiceListType type)
    {
        var path = FilePath(name, type);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}
