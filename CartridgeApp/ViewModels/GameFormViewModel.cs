using System.ComponentModel;
using System.Runtime.CompilerServices;

using CartridgeApp.Models;

namespace CartridgeApp.ViewModels;

/// <summary>
/// View model used to edit game details in the add/edit form.
/// </summary>
public class GameFormViewModel : INotifyPropertyChanged
{
    private int _id;
    private string _title = string.Empty;
    private string _developer = string.Empty;
    private string _platform = "PC";
    private GameStatus _status = GameStatus.Backlog;
    private int? _rating;
    private int _hoursPlayed;
    private string _notes = string.Empty;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Developer
    {
        get => _developer;
        set => SetProperty(ref _developer, value);
    }

    public string Platform
    {
        get => _platform;
        set => SetProperty(ref _platform, value);
    }

    public GameStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public int? Rating
    {
        get => _rating;
        set => SetProperty(ref _rating, value);
    }

    public int HoursPlayed
    {
        get => _hoursPlayed;
        set => SetProperty(ref _hoursPlayed, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public IReadOnlyList<string> Platforms { get; } =
    [
        "PC", "Steam Deck", "PlayStation", "Xbox", "Nintendo Switch", "Mobile"
    ];

    public IReadOnlyList<GameStatus> Statuses { get; } = Enum.GetValues<GameStatus>();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
