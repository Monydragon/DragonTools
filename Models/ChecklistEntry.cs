using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DragonTools.Models;

public sealed class ChecklistEntry : INotifyPropertyChanged
{
    string _text = string.Empty;
    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; OnPropertyChanged(); } }
    }

    bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set { if (_isDone != value) { _isDone = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
