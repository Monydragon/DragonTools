using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Maui.Storage;
using DragonTools.Interfaces;
using DragonTools.Models;

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

    public RandomPickerVm()
    {
        Items = new ObservableCollection<IChoice>();
        PagedItems = new ObservableCollection<IChoice>();
        SavedListDisplay = new ObservableCollection<string>();
        AllSavedLists = new ObservableCollection<string>();
        PageSizeOptions = new ObservableCollection<int> { 5, 10, 20, 50 };
        
        // Await LoadSavedListsAsync to ensure dropdown is populated
        _ = LoadSavedListsAsync();
        UpdatePagedItems();
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
            _selectedList = value ?? string.Empty; // Removed redundant null check
            OnPropertyChanged();
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

            var appDataPath = FileSystem.AppDataDirectory;
            var pickerFolder = Path.Combine(appDataPath, "RandomPicker");
            
            if (!Directory.Exists(pickerFolder))
            {
                Directory.CreateDirectory(pickerFolder);
            }

            var fileName = SanitizeFileName(listName);
            var typePrefix = IsWeightedMode ? "weighted" : "normal";
            var filePath = Path.Combine(pickerFolder, $"{fileName}.{typePrefix}.json");

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
            await File.WriteAllTextAsync(filePath, json);

            // Refresh the lists
            await LoadSavedListsAsync();
            // Select the saved list in the dropdown
            SelectedList = listName;
            FilterSavedLists();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving list: {ex.Message}");
            return false;
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

            // Try to find the file (check both normal and weighted versions)
            var normalPath = Path.Combine(pickerFolder, $"{fileName}.normal.json");
            var weightedPath = Path.Combine(pickerFolder, $"{fileName}.weighted.json");

            string? filePath = File.Exists(normalPath) ? normalPath : File.Exists(weightedPath) ? weightedPath : null;

            if (filePath == null)
            {
                System.Diagnostics.Debug.WriteLine($"No file found for list: {listName}");
                return false;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var listData = JsonSerializer.Deserialize<SavedListData>(json);

            if (listData != null)
            {
                ListName = listData.Name ?? listName;
                IsWeightedMode = listData.Type == "Weighted";

                Items.Clear();
                if (listData.Items != null)
                {
                    foreach (var item in listData.Items)
                    {
                        if (IsWeightedMode)
                        {
                            Items.Add(new NormalChoice( item.Entry ?? "", item.Weight));
                        }
                        else
                        {
                            Items.Add(new NormalChoice(item.Entry ?? ""));
                        }
                    }
                }

                UpdatePagedItems();
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
            var (entry, weight) = ParseEntryWithWeight(item);
            if (IsWeightedMode)
                Items.Add(new NormalChoice(entry, weight));
            else
                Items.Add(new NormalChoice(entry));
        }

        BulkText = "";
        UpdatePagedItems();
    }

    private (string entry, int weight) ParseEntryWithWeight(string input)
    {
        // Only support the format: Name[weight]; everything else -> weight=1, entry trimmed
        if (string.IsNullOrWhiteSpace(input))
            return (string.Empty, 1);

        var entry = input.Trim();
        var weight = 1;

        var match = System.Text.RegularExpressions.Regex.Match(entry, "^(.+?)\\[(\\d+)\\]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            entry = match.Groups[1].Value.Trim();
            if (int.TryParse(match.Groups[2].Value, out var parsed))
                weight = Math.Max(1, parsed);
        }

        return (entry, weight);
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
    }

    public void RemoveItem(IChoice item)
    {
        Items.Remove(item);
        UpdatePagedItems();
    }

    public void ClearItems()
    {
        Items.Clear();
        UpdatePagedItems();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
