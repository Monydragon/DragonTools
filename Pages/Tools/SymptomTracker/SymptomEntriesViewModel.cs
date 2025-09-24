using System;
using System.Collections.ObjectModel;
using System.Linq;
using DragonTools.Models.Symptom_Tracker;

namespace DragonTools.Pages.Tools.SymptomTracker;

public class SymptomEntryGroup : ObservableCollection<SymptomHandler>
{
    public DateTime Date { get; }
    public string DisplayDate => Date.ToString("yyyy-MM-dd");
    public SymptomEntryGroup(DateTime date, System.Collections.Generic.IEnumerable<SymptomHandler> entries)
        : base(entries)
    {
        Date = date;
    }
}

public class SymptomEntriesViewModel
{
    private SymptomJournal _journal;
    public ObservableCollection<SymptomEntryGroup> GroupedEntries { get; set; }

    public SymptomEntriesViewModel(SymptomJournal journal)
    {
        _journal = journal;
        GroupedEntries = new ObservableCollection<SymptomEntryGroup>(
            _journal.GetAllEntries()
                .GroupBy(e => e.Date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new SymptomEntryGroup(g.Key, g))
        );
    }

    public void Refresh()
    {
        GroupedEntries.Clear();
        foreach (var group in _journal.GetAllEntries()
            .GroupBy(e => e.Date.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new SymptomEntryGroup(g.Key, g)))
        {
            GroupedEntries.Add(group);
        }
    }

    public void DeleteEntry(SymptomHandler entry)
    {
        _journal.RemoveEntry(entry);
        Refresh();
    }

    public void DeleteEntryGroup(SymptomEntryGroup group)
    {
        var date = group.Date.Date;
        var toRemove = _journal.GetEntriesByDate(date).ToList();
        foreach (var entry in toRemove)
        {
            _journal.RemoveEntry(entry);
        }
        Refresh();
    }
}
