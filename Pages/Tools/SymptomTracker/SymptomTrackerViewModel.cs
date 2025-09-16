using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DragonTools.Enums;
using DragonTools.Models.Symptom_Tracker;

namespace DragonTools.Pages.Tools.SymptomTracker;

public class SymptomCheckbox : INotifyPropertyChanged
{
    public Symptoms Symptom { get; set; }
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }
    }
    private SymptomSeverity _severity = SymptomSeverity.Mild;
    public SymptomSeverity Severity
    {
        get => _severity;
        set
        {
            if (_severity != value)
            {
                _severity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Severity)));
            }
        }
    }
    public ObservableCollection<SymptomSeverity> Severities { get; } = new ObservableCollection<SymptomSeverity>(Enum.GetValues(typeof(SymptomSeverity)).Cast<SymptomSeverity>());
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SymptomTrackerViewModel : INotifyPropertyChanged
{
    public ObservableCollection<SymptomCheckbox> SymptomsList { get; set; }
    public ObservableCollection<SymptomSeverity> Severities { get; set; } = new ObservableCollection<SymptomSeverity>(Enum.GetValues(typeof(SymptomSeverity)).Cast<SymptomSeverity>());
    private SymptomJournal _journal;
    public event PropertyChangedEventHandler? PropertyChanged;

    public SymptomTrackerViewModel()
    {
        _journal = new SymptomJournal();
        SymptomsList = new ObservableCollection<SymptomCheckbox>(
            Enum.GetValues(typeof(Symptoms)).Cast<Symptoms>().Select(s => new SymptomCheckbox
            {
                Symptom = s,
                IsChecked = false // Always start unchecked
            })
        );
        foreach (var item in SymptomsList)
        {
            item.PropertyChanged += (s, e) => OnSymptomCheckedChanged(item);
        }
    }

    private bool IsSymptomLoggedToday(Symptoms symptom)
    {
        return _journal.GetEntriesByDate(DateTime.Today).Any(e => e.Symptom == symptom);
    }

    private void OnSymptomCheckedChanged(SymptomCheckbox item)
    {
        if (item.IsChecked)
        {
            // Add entry for today with selected severity and empty notes
            _journal.AddEntry(new SymptomHandler(item.Symptom, DateTime.Today, item.Severity, ""));
        }
        else
        {
            // Remove entry for today
            var entry = _journal.GetEntriesByDate(DateTime.Today).FirstOrDefault(e => e.Symptom == item.Symptom);
            if (entry != null)
            {
                _journal.RemoveEntry(entry);
            }
        }
    }

    public void SaveEntries()
    {
        foreach (var item in SymptomsList.Where(x => x.IsChecked))
        {
            // Remove any existing entry for today and this symptom
            var existing = _journal.GetEntriesByDate(DateTime.Today).FirstOrDefault(e => e.Symptom == item.Symptom);
            if (existing != null)
                _journal.RemoveEntry(existing);
            // Add new entry with selected severity
            _journal.AddEntry(new SymptomHandler(item.Symptom, DateTime.Today, item.Severity, ""));
        }
    }

    public SymptomJournal Journal => _journal;
}
