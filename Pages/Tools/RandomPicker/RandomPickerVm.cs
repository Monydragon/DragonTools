using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using DragonTools.Interfaces;
using DragonTools.Models;
using System.Collections.Specialized; // added
using System.Threading; // added
using System.Threading.Tasks; // added

namespace DragonTools.Pages.Tools.RandomPicker;

public class RandomPickerVm : INotifyPropertyChanged
{
    private bool _entriesExpanded = true;
    private string _searchText = "";
    private string _bulkText = "";
    private string _listName = ""; // start empty instead of "New List"
    private string _typeBadge = "[Normal]";
    private bool _isWeightedMode;
    private int _pageSize = 5; // default to 5
    private int _currentPage;
    private string _listSearchText = "";
    private string _selectedList = string.Empty;
    private int _defaultWeight = 1; // default weight for weighted mode when none specified

    // Auto-save infrastructure
    private const int AutoSaveDelayMs = 1000; // debounce delay
    private CancellationTokenSource? _autoSaveCts;
    private bool _suppressAutoSave; // suppress during programmatic loads
    private bool _performingSave; // prevent recursion

    public RandomPickerVm()
    {
        Items = new ObservableCollection<IChoice>();
        PagedItems = new ObservableCollection<IChoice>();
        SavedListDisplay = new ObservableCollection<string>();
        AllSavedLists = new ObservableCollection<string>();
        PageSizeOptions = new ObservableCollection<int> { 5, 10, 20, 50 };
        
        // Wire collection change tracking for auto-save
        Items.CollectionChanged += Items_CollectionChanged;
        
        // Await LoadSavedListsAsync to ensure dropdown is populated
        _ = LoadSavedListsAsync();
        UpdatePagedItems();
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Immediate save for structural entry changes
        if (e.OldItems != null)
        {
            foreach (var obj in e.OldItems)
            {
                if (obj is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= Item_PropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (var obj in e.NewItems)
            {
                if (obj is INotifyPropertyChanged npc)
                    npc.PropertyChanged += Item_PropertyChanged;
            }
        }
        // Immediate save for structural entry changes
        ScheduleAutoSave(immediate:true);
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IChoice.Entry) || e.PropertyName == nameof(IChoice.Weight))
            ScheduleAutoSave(immediate:true);
    }

    private void ScheduleAutoSave(bool immediate = false)
    {
        if (_suppressAutoSave || _performingSave)
            return;
        if (string.IsNullOrWhiteSpace(ListName) || ListName == "New List")
            return; // do not auto-save unnamed/new lists

        if (immediate)
        {
            _autoSaveCts?.Cancel();
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await SaveListAsync(ListName);
            });
            return;
        }

        _autoSaveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _autoSaveCts = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(AutoSaveDelayMs, token);
                if (token.IsCancellationRequested) return;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await SaveListAsync(ListName); // ignore result silently
                });
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save error: {ex.Message}");
            }
        }, token);
    }

    // Collections
    public ObservableCollection<IChoice> Items { get; set; }
    public ObservableCollection<IChoice> PagedItems { get; set; }
    public ObservableCollection<string> SavedListDisplay { get; set; }
    public ObservableCollection<string> AllSavedLists { get; set; }
    public ObservableCollection<int> PageSizeOptions { get; set; }

    // Properties for collapsible UI
    public bool EntriesExpanded
    {
        get => _entriesExpanded;
        set
        {
            _entriesExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EntriesChevron));
        }
    }

    public string EntriesChevron => EntriesExpanded ? "▼" : "▶";

    // Search and filtering for entries
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            FilterItems();
        }
    }

    // Search functionality for lists
    public string ListSearchText
    {
        get => _listSearchText;
        set
        {
            _listSearchText = value;
            OnPropertyChanged();
            FilterSavedLists();
        }
    }

    // Bulk operations
    public string BulkText
    {
        get => _bulkText;
        set
        {
            _bulkText = value;
            OnPropertyChanged();
        }
    }

    // List management
    public string ListName
    {
        get => _listName;
        set
        {
            _listName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ListStatusText)); // Ensure status updates
            ScheduleAutoSave();
        }
    }

    public string TypeBadge
    {
        get => _typeBadge;
        set
        {
            _typeBadge = value;
            OnPropertyChanged();
        }
    }

    public bool IsWeightedMode
    {
        get => _isWeightedMode;
        set
        {
            _isWeightedMode = value;
            OnPropertyChanged();
            TypeBadge = value ? "[Weighted]" : "[Normal]";
            UpdatePagedItems();
            ScheduleAutoSave();
        }
    }

    public int DefaultWeight
    {
        get => _defaultWeight;
        set
        {
            var v = Math.Max(1, value);
            if (_defaultWeight == v) return;
            _defaultWeight = v;
            OnPropertyChanged();
            ScheduleAutoSave();
        }
    }

    // Pagination
    public int PageSize
    {
        get => _pageSize;
        set
        {
            _pageSize = value;
            OnPropertyChanged();
            _currentPage = 0;
            UpdatePagedItems();
        }
    }

    public string PageLabel
    {
        get
        {
            var totalPages = (int)Math.Ceiling((double)FilteredItems.Count / PageSize);
            return totalPages == 0 ? "0 / 0" : $"{_currentPage + 1} / {totalPages}";
        }
    }

    public string SelectedList
    {
        get => _selectedList;
        set
        {
            if (_selectedList == value) return;
            _selectedList = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ListName)); // Ensure ListName updates
            OnPropertyChanged(nameof(TypeBadge)); // Ensure TypeBadge updates
            OnPropertyChanged(nameof(ListStatusText)); // Ensure status updates
            OnPropertyChanged(nameof(Items)); // Ensure item count updates

            if (!string.IsNullOrWhiteSpace(_selectedList))
            {
                _ = LoadListFromSelectionAsync(_selectedList);
            }
        }
    }

    private async Task LoadListFromSelectionAsync(string listName)
    {
        try
        {
            var ok = await LoadListAsync(listName);
            if (ok)
            {
                // keep SelectedList synchronized with loaded name
                if (!string.Equals(SelectedList, listName, StringComparison.Ordinal))
                    SelectedList = listName;
            }
        }
        catch { /* swallow, errors already logged in LoadListAsync */ }
    }

    private List<IChoice> FilteredItems
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return Items.ToList();

            return Items.Where(item => item.Entry.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    // Methods
    public void ToggleEntries()
    {
        EntriesExpanded = !EntriesExpanded;
    }

    public void FilterItems()
    {
        _currentPage = 0;
        UpdatePagedItems();
    }

    public void FilterSavedLists()
    {
        SavedListDisplay.Clear();
        foreach (var list in AllSavedLists)
        {
            SavedListDisplay.Add(list);
        }
    }

    public async Task LoadSavedListsAsync()
    {
        try
        {
            AllSavedLists.Clear();
            SavedListDisplay.Clear();

            // Load saved lists from storage
            var savedLists = await GetSavedListsFromStorageAsync();
            foreach (var list in savedLists)
            {
                AllSavedLists.Add(list);
            }

            FilterSavedLists();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading saved lists: {ex.Message}");
        }
    }

    private async Task<List<string>> GetSavedListsFromStorageAsync()
    {
        try
        {
            var lists = new List<string>();
            var appDataPath = FileSystem.AppDataDirectory;
            var pickerFolder = Path.Combine(appDataPath, "RandomPicker");

            if (Directory.Exists(pickerFolder))
            {
                var files = Directory.GetFiles(pickerFolder, "*.json");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Remove type suffix (.normal or .weighted)
                    if (fileName.EndsWith(".normal") || fileName.EndsWith(".weighted"))
                    {
                        var lastDot = fileName.LastIndexOf('.');
                        if (lastDot > 0)
                        {
                            fileName = fileName.Substring(0, lastDot);
                        }
                    }

                    if (!lists.Contains(fileName))
                    {
                        lists.Add(fileName);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"Retrieved saved lists: {string.Join(", ", lists)}");
            return lists.OrderBy(x => x).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error retrieving saved lists: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<bool> SaveListAsync(string listName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(listName))
                return false;

            _performingSave = true;
            var appDataPath = FileSystem.AppDataDirectory;
            var pickerFolder = Path.Combine(appDataPath, "RandomPicker");
            
            if (!Directory.Exists(pickerFolder))
            {
                Directory.CreateDirectory(pickerFolder);
            }

            var fileName = SanitizeFileName(listName);
            var desiredTypePrefix = IsWeightedMode ? "weighted" : "normal";
            var desiredPath = Path.Combine(pickerFolder, $"{fileName}.{desiredTypePrefix}.json");
            var otherPath = Path.Combine(pickerFolder, $"{fileName}.{(IsWeightedMode ? "normal" : "weighted")}.json");

            if (File.Exists(otherPath))
            {
                try { File.Delete(otherPath); } catch { /* ignore */ }
            }

            var listData = new SavedListData
            {
                Name = listName,
                Type = IsWeightedMode ? "Weighted" : "Normal",
                Items = Items.Select(item => new SavedItem
                {
                    Entry = item.Entry,
                    Weight = item is IChoice weighted ? weighted.Weight : 1
                }).ToList()
            };

            var json = JsonSerializer.Serialize(listData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(desiredPath, json);

            await LoadSavedListsAsync();
            SelectedList = listName;
            FilterSavedLists();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving list: {ex.Message}");
            return false;
        }
        finally
        {
            _performingSave = false;
        }
    }

    public async Task<bool> LoadListAsync(string listName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(listName))
            {
                System.Diagnostics.Debug.WriteLine("LoadListAsync called with an empty list name.");
                return false; // no-ops on empty selection
            }

            var appDataPath = FileSystem.AppDataDirectory;
            var pickerFolder = Path.Combine(appDataPath, "RandomPicker");
            var fileName = SanitizeFileName(listName);

            var normalPath = Path.Combine(pickerFolder, $"{fileName}.normal.json");
            var weightedPath = Path.Combine(pickerFolder, $"{fileName}.weighted.json");

            string? filePath = null;
            var normalExists = File.Exists(normalPath);
            var weightedExists = File.Exists(weightedPath);
            if (normalExists && weightedExists)
            {
                filePath = IsWeightedMode ? weightedPath : normalPath;
            }
            else if (normalExists)
            {
                filePath = normalPath;
            }
            else if (weightedExists)
            {
                filePath = weightedPath;
            }

            if (filePath == null)
            {
                System.Diagnostics.Debug.WriteLine($"No file found for list: {listName}");
                return false;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var listData = JsonSerializer.Deserialize<SavedListData>(json);

            if (listData != null)
            {
                _suppressAutoSave = true; // prevent auto-save during load
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ListName = listData.Name ?? listName;
                    IsWeightedMode = listData.Type == "Weighted";

                    Items.Clear();
                    if (listData.Items != null)
                    {
                        foreach (var item in listData.Items)
                        {
                            var entryText = item.Entry ?? "";
                            var w = item.Weight > 0 ? item.Weight : Math.Max(1, DefaultWeight);
                            Items.Add(new NormalChoice(entryText, w));
                        }
                    }

                    UpdatePagedItems();
                    NotifyInfoCard();
                });
                _suppressAutoSave = false; // re-enable
                System.Diagnostics.Debug.WriteLine($"Successfully loaded list: {listName}");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"Failed to deserialize list data for: {listName}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading list {listName}: {ex.Message}");
            return false;
        }
    }

    public void CreateNewList(string name, bool weighted)
    {
        ListName = name?.Trim() ?? string.Empty;
        IsWeightedMode = weighted;
        ClearItems();
        // Add to SavedListDisplay and select
        if (!string.IsNullOrWhiteSpace(ListName))
        {
            if (!AllSavedLists.Contains(ListName))
            {
                AllSavedLists.Add(ListName);
                FilterSavedLists();
            }
            SelectedList = ListName;
        }
    }

    public async Task<bool> DeleteListAsync(string listName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(listName) || listName == "New List")
                return false;

            var appDataPath = FileSystem.AppDataDirectory;
            var pickerFolder = Path.Combine(appDataPath, "RandomPicker");
            var fileName = SanitizeFileName(listName);

            var normalPath = Path.Combine(pickerFolder, $"{fileName}.normal.json");
            var weightedPath = Path.Combine(pickerFolder, $"{fileName}.weighted.json");

            bool deleted = false;
            if (File.Exists(normalPath))
            {
                File.Delete(normalPath);
                deleted = true;
            }
            if (File.Exists(weightedPath))
            {
                File.Delete(weightedPath);
                deleted = true;
            }

            if (deleted)
            {
                await LoadSavedListsAsync();
                FilterSavedLists();
                SelectedList = string.Empty;
                ListName = "New List";
                ClearItems();
            }

            return deleted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting list: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RenameListAsync(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return false;
        // Save under new name and delete old
        var saved = await SaveListAsync(newName);
        if (saved)
        {
            await DeleteListAsync(oldName);
            SelectedList = newName;
            FilterSavedLists();
        }
        return saved;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    public void UpdatePagedItems()
    {
        var filtered = FilteredItems;
        var skip = _currentPage * PageSize;
        var take = PageSize;

        PagedItems.Clear();
        foreach (var item in filtered.Skip(skip).Take(take))
        {
            PagedItems.Add(item);
        }

        OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(Items)); // Ensure item count updates
        OnPropertyChanged(nameof(ListStatusText)); // Ensure status updates
    }

    public void NextPage()
    {
        var totalPages = (int)Math.Ceiling((double)FilteredItems.Count / PageSize);
        if (_currentPage < totalPages - 1)
        {
            _currentPage++;
            UpdatePagedItems();
        }
    }

    public void PreviousPage()
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            UpdatePagedItems();
        }
    }

    public void AddBulkItems()
    {
        if (string.IsNullOrWhiteSpace(BulkText))
            return;

        var lines = BulkText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var items = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                var commaSplit = trimmed.Split(',');
                foreach (var item in commaSplit)
                {
                    var cleanItem = item.Trim();
                    if (!string.IsNullOrEmpty(cleanItem))
                        items.Add(cleanItem);
                }
            }
        }

        foreach (var item in items)
        {
            var parsed = ParseEntryWithWeight(item);
            var entry = parsed.entry;
            var weight = parsed.weight;
            var specified = parsed.specified;
            if (IsWeightedMode)
                Items.Add(new NormalChoice(entry, specified ? weight : Math.Max(1, DefaultWeight)));
            else
                Items.Add(new NormalChoice(entry));
        }

        BulkText = "";
        UpdatePagedItems();
        ScheduleAutoSave(immediate:true);
    }

    private (string entry, int weight, bool specified) ParseEntryWithWeight(string input)
    {
        // Only support the format: Name[weight]; everything else -> weight=1, entry trimmed
        if (string.IsNullOrWhiteSpace(input))
            return (string.Empty, 1, false);

        var entry = input.Trim();
        var weight = 1;
        var specified = false;

        var match = System.Text.RegularExpressions.Regex.Match(entry, "^(.+?)\\[(\\d+)\\]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            entry = match.Groups[1].Value.Trim();
            if (int.TryParse(match.Groups[2].Value, out var parsed))
                weight = Math.Max(1, parsed);
            specified = true;
        }

        return (entry, weight, specified);
    }

    public IChoice? RollRandom()
    {
        var items = FilteredItems;
        if (items.Count == 0)
            return null;

        var random = new Random();

        if (IsWeightedMode)
        {
            var weightedItems = items.OfType<NormalChoice>().ToList();
            if (weightedItems.Count == 0)
                return items[random.Next(items.Count)];

            var totalWeight = weightedItems.Sum(w => w.Weight);
            var randomWeight = random.Next(1, totalWeight + 1);
            var currentWeight = 0;

            foreach (var item in weightedItems)
            {
                currentWeight += item.Weight;
                if (currentWeight >= randomWeight)
                    return item;
            }
        }

        return items[random.Next(items.Count)];
    }

    public void AddItem(string entry, int weight = 1)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return;

        if (IsWeightedMode)
        {
            Items.Add(new NormalChoice(entry.Trim(), Math.Max(1, weight)));
        }
        else
        {
            Items.Add(new NormalChoice(entry.Trim()));
        }

        UpdatePagedItems();
        ScheduleAutoSave(immediate:true);
    }

    public void RemoveItem(IChoice item)
    {
        Items.Remove(item);
        UpdatePagedItems();
        ScheduleAutoSave(immediate:true);
    }

    public void ClearItems()
    {
        Items.Clear();
        UpdatePagedItems();
        ScheduleAutoSave(immediate:true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string ListStatusText
    {
        get
        {
            if (Items == null || Items.Count == 0)
            {
                return "Empty";
            }
            return "Ready";
        }
    }

    public void NotifyInfoCard()
    {
        OnPropertyChanged(nameof(ListName));
        OnPropertyChanged(nameof(TypeBadge));
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(ListStatusText));
    }
}

// Data classes for JSON serialization
public class SavedListData
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public List<SavedItem>? Items { get; set; }
}

public class SavedItem
{
    public string? Entry { get; set; }
    public int Weight { get; set; } = 1;
}
