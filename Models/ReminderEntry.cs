using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DragonTools.Models;

public sealed class ReminderEntry : INotifyPropertyChanged
{
    DateTime _when = DateTime.Now.AddHours(1);
    public DateTime When
    {
        get => _when;
        set { if (_when != value) { _when = value; OnPropertyChanged(); } }
    }

    string? _title;
    public string? Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
