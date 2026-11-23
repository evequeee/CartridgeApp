using System.ComponentModel;
using System.Runtime.CompilerServices;

using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// View model used to edit game details in the add/edit form.
/// </summary>
public class GameFormViewModel : INotifyPropertyChanged
{
    private readonly IGameRepository _gameRepository;
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

    public ICommand SaveGameCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameFormViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        SaveGameCommand = new Command(async () => await SaveGameAsync());
    }

    public void LoadGame(Game game)
    {
        Id = game.Id;
        Title = game.Title;
        Developer = game.Developer;
        Platform = game.Platform;
        Status = game.Status;
        Rating = game.Rating;
        HoursPlayed = game.HoursPlayed;
        Notes = game.Notes;
    }

    private async Task SaveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await ShowAlertAsync("Помилка", "Назва гри є обов'язковою.");
            return;
        }

        if (Rating.HasValue && (Rating.Value < 1 || Rating.Value > 10))
        {
            await ShowAlertAsync("Помилка", "Рейтинг має бути в діапазоні від 1 до 10.");
            return;
        }

        if (HoursPlayed < 0)
        {
            await ShowAlertAsync("Помилка", "Кількість годин не може бути від'ємною.");
            return;
        }

        var game = new Game
        {
            Id = Id,
            Title = Title.Trim(),
            Developer = string.IsNullOrWhiteSpace(Developer) ? "Unknown" : Developer.Trim(),
            Platform = Platform,
            Status = Status,
            Rating = Rating,
            HoursPlayed = HoursPlayed,
            Notes = Notes.Trim()
        };

        if (game.Id == 0)
        {
            await _gameRepository.AddGameAsync(game);
        }
        else
        {
            await _gameRepository.UpdateGameAsync(game);
        }

        await Shell.Current.GoToAsync("..");
    }

    private static async Task ShowAlertAsync(string title, string message)
    {
        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync(title, message, "OK");
        }
    }

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
