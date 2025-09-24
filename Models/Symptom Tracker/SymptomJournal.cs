using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DragonTools.Enums;

namespace DragonTools.Models.Symptom_Tracker;

public class SymptomJournal
{
    private readonly List<SymptomHandler> _entries = new();
    private readonly string _storagePath;

    public SymptomJournal(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "symptom_journal.json");
        LoadFromFile();
    }

    public void AddEntry(SymptomHandler entry)
    {
        // Remove any existing entry for the same symptom and date
        _entries.RemoveAll(e => e.Symptom == entry.Symptom && e.Date.Date == entry.Date.Date);
        _entries.Add(entry);
        SaveToFile();
    }

    public void RemoveEntry(SymptomHandler entry)
    {
        _entries.Remove(entry);
        SaveToFile();
    }

    public IEnumerable<SymptomHandler> GetAllEntries()
    {
        return _entries;
    }

    public IEnumerable<SymptomHandler> GetEntriesByDate(DateTime date)
    {
        return _entries.Where(e => e.Date.Date == date.Date);
    }

    public IEnumerable<SymptomHandler> GetEntriesBySymptom(Symptoms symptom)
    {
        return _entries.Where(e => e.Symptom == symptom);
    }

    public IEnumerable<SymptomHandler> GetEntriesBySeverity(SymptomSeverity severity)
    {
        return _entries.Where(e => e.Severity == severity);
    }

    private void SaveToFile()
    {
        var json = JsonSerializer.Serialize(_entries);
        File.WriteAllText(_storagePath, json);
    }

    private void LoadFromFile()
    {
        if (File.Exists(_storagePath))
        {
            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<SymptomHandler>>(json);
            if (loaded != null)
            {
                _entries.Clear();
                _entries.AddRange(loaded);
            }
        }
    }
}
