using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DragonTools.Models;

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

    // UI events that delegate to VM or helper methods

    private void AddItem_Clicked(object sender, EventArgs e) => _vm.AddNewItem();

    private async void EditItem_Clicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is ChoiceItem item)
        {
            var newName = await DisplayPromptAsync("Edit Choice", "Name:", initialValue: item.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            int newWeight = item.Weight;
            if (_vm.IsWeightedMode)
            {
                var weightStr = await DisplayPromptAsync("Edit Choice", "Weight:", initialValue: item.Weight.ToString());
                if (int.TryParse(weightStr, out var w) && w > 0) newWeight = w;
            }
            _vm.EditItem(item, newName!, newWeight);
        }
    }

    private void DeleteItem_Clicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is ChoiceItem item) _vm.DeleteItem(item);
    }

    private void SearchBar_TextChanged(object sender, TextChangedEventArgs e) => _vm.ApplyFilter();

    private async void Roll_Clicked(object sender, EventArgs e) => await _vm.RollAsync();

    private async void Save_Clicked(object sender, EventArgs e)
    {
        var ok = await _vm.SaveAsync();
        if (!ok) await DisplayAlert("Save Failed", "Please enter a list name and at least one choice.", "OK");
        else await DisplayAlert("Saved", $"Saved '{_vm.ListName}'.", "OK");
    }

    private async void Load_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ListName))
        {
            await DisplayAlert("Load", "Enter the list name or pick one below.", "OK");
            return;
        }
        var loaded = await _vm.LoadAsync(_vm.ListName);
        await DisplayAlert(loaded ? "Loaded" : "Not found",
            loaded ? $"Loaded '{_vm.ListName}'." : "That list wasn't found.", "OK");
    }

    private async void SavedList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (sender is not Picker p || p.SelectedIndex < 0) return;
        var selected = _vm.SavedListDisplay[p.SelectedIndex];
        var name = ListStorageService.ParseDisplayName(selected, out var type);
        _vm.SelectedListType = type;
        await _vm.LoadAsync(name);
    }

    private void RefreshSaved_Clicked(object sender, EventArgs e) => _vm.RefreshSavedLists();
}

/* =======================
 * ViewModel & helpers
 * ======================= */

public enum ChoiceListType { Normal, Weighted }

public class ChoiceItem : INotifyPropertyChanged
{
    string _name = "";
    int _weight = 1;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    // Always present for binding; ignored in Normal mode
    public int Weight { get => _weight; set { _weight = value < 1 ? 1 : value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));
}

public class RandomPickerVm : INotifyPropertyChanged
{
    public ObservableCollection<ChoiceItem> Items { get; } = new();
    public ObservableCollection<ChoiceItem> FilteredItems { get; } = new();

    public ObservableCollection<string> ListTypes { get; } =
        new(new[] { nameof(ChoiceListType.Normal), nameof(ChoiceListType.Weighted) });

    ChoiceListType _selectedListType = ChoiceListType.Normal;
    public ChoiceListType SelectedListType
    {
        get => _selectedListType;
        set { _selectedListType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsWeightedMode)); ApplyFilter(); }
    }

    public bool IsWeightedMode => SelectedListType == ChoiceListType.Weighted;

    string _listName = "";
    public string ListName { get => _listName; set { _listName = value; OnPropertyChanged(); } }

    string _searchText = "";
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); } }

    string _newItemName = "";
    public string NewItemName { get => _newItemName; set { _newItemName = value; OnPropertyChanged(); } }

    string _newItemWeight = "1";
    public string NewItemWeight { get => _newItemWeight; set { _newItemWeight = value; OnPropertyChanged(); } }

    string _lastResult = "—";
    public string LastResult { get => _lastResult; set { _lastResult = value; OnPropertyChanged(); } }

    public ObservableCollection<string> SavedListDisplay { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));

    public void AddNewItem()
    {
        var name = (NewItemName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        int weight = 1;
        if (IsWeightedMode && int.TryParse(NewItemWeight, out var w) && w > 0) weight = w;

        Items.Add(new ChoiceItem { Name = name, Weight = weight });
        NewItemName = "";
        NewItemWeight = "1";
        ApplyFilter();
    }

    public void EditItem(ChoiceItem item, string newName, int newWeight)
    {
        item.Name = newName.Trim();
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
            : Items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredItems.Clear();
        foreach (var i in filtered) FilteredItems.Add(i);
    }

    public async Task RollAsync()
    {
        if (Items.Count == 0) { LastResult = "No items."; return; }

        string picked;
        if (IsWeightedMode)
        {
            // Convert to WeightedChoice and roll
            var list = Items.Select(i => new WeightedChoice(i.Name, i.Weight)).ToList(); // uses your model
            picked = RollWeighted(list);
        }
        else
        {
            var list = Items.Select(i => new NormalChoice(i.Name)).ToList(); // uses your model
            var idx = Random.Shared.Next(0, list.Count);
            picked = list[idx].Name;
        }

        LastResult = $"Result: {picked}";
        await Task.CompletedTask;
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

    public async Task<bool> SaveAsync()
    {
        if (Items.Count == 0 || string.IsNullOrWhiteSpace(ListName)) return false;

        var dto = new ChoiceListDto
        {
            Name = ListName.Trim(),
            Type = SelectedListType.ToString(),
            Items = Items.Select(i => new ChoiceDto { Name = i.Name, Weight = i.Weight }).ToList()
        };
        await ListStorageService.SaveAsync(dto);
        RefreshSavedLists();
        return true;
    }

    public async Task<bool> LoadAsync(string name)
    {
        var dto = await ListStorageService.LoadAsync(name.Trim(), SelectedListType);
        if (dto is null) return false;

        ListName = dto.Name;
        SelectedListType = Enum.TryParse<ChoiceListType>(dto.Type, out var t) ? t : ChoiceListType.Normal;

        Items.Clear();
        foreach (var c in dto.Items ?? [])
            Items.Add(new ChoiceItem { Name = c.Name ?? "", Weight = Math.Max(1, c.Weight ?? 1) });
        ApplyFilter();
        LastResult = "—";
        return true;
    }

    public void RefreshSavedLists()
    {
        SavedListDisplay.Clear();
        foreach (var s in ListStorageService.ListSavedDisplays())
            SavedListDisplay.Add(s);
    }
}

/* =======================
 * Storage DTOs & service
 * ======================= */

public class ChoiceListDto
{
    public string? Name { get; set; }
    public string? Type { get; set; } // "Normal" | "Weighted"
    public List<ChoiceDto>? Items { get; set; }
}

public class ChoiceDto
{
    public string? Name { get; set; }
    public int? Weight { get; set; } // optional in Normal
}

public static class ListStorageService
{
    static readonly string Root = FileSystem.AppDataDirectory;
    static readonly string Folder = Path.Combine(Root, "RandomPicker");

    static ListStorageService()
    {
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
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
        var json = System.Text.Json.JsonSerializer.Serialize(dto,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<ChoiceListDto?> LoadAsync(string name, ChoiceListType type)
    {
        var path = FilePath(name, type);
        if (!File.Exists(path))
        {
            // If type wrong, try the other one for convenience
            var other = type == ChoiceListType.Normal ? ChoiceListType.Weighted : ChoiceListType.Normal;
            var alt = FilePath(name, other);
            if (!File.Exists(alt)) return null;
            path = alt;
        }

        var json = await File.ReadAllTextAsync(path);
        return System.Text.Json.JsonSerializer.Deserialize<ChoiceListDto>(json);
    }

    public static string[] ListSavedDisplays()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<string>();
        var files = Directory.GetFiles(Folder, "*.json");
        // Display as "Name (Type)"
        return files.Select(f =>
        {
            var file = Path.GetFileNameWithoutExtension(f);
            // format: "{name}.{type}"
            var lastDot = file.LastIndexOf('.');
            if (lastDot < 0) return file;
            var name = file[..lastDot];
            var type = file[(lastDot + 1)..];
            return $"{name} ({type})";
        }).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string ParseDisplayName(string display, out ChoiceListType type)
    {
        // "MyList (weighted)" or "MyList (normal)"
        var open = display.LastIndexOf('(');
        var close = display.LastIndexOf(')');
        string n = display;
        type = ChoiceListType.Normal;

        if (open >= 0 && close > open)
        {
            n = display[..(open)].Trim();
            var t = display.Substring(open + 1, close - open - 1);
            type = t.Equals("weighted", StringComparison.OrdinalIgnoreCase) ? ChoiceListType.Weighted : ChoiceListType.Normal;
        }
        return n;
    }
}