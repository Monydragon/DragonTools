using System.ComponentModel;
using DragonTools.Interfaces;

namespace DragonTools.Models;

public class NormalChoice : IChoice, INotifyPropertyChanged
{
    private string _entry = "";
    private int _weight = 1;

    public string Entry
    {
        get => _entry;
        set
        {
            if (_entry == value) return;
            _entry = value ?? "";
            OnPropertyChanged(nameof(Entry));
        }
    }

    public int Weight
    {
        get => _weight;
        set
        {
            var v = value < 1 ? 1 : value;
            if (_weight == v) return;
            _weight = v;
            OnPropertyChanged(nameof(Weight));
        }
    }
    
    public NormalChoice() { }
    
    public NormalChoice(string entry)
    {
        Entry = entry;
    }
    
    public NormalChoice(string entry, int weight)
    {
        Entry = entry;
        Weight = weight;
    }
    public override string ToString() => Entry;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}