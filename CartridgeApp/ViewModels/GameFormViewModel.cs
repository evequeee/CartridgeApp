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
    private const double RatingStep = 0.5;
    private const double MaxRating = 5.0;

    private readonly IGameRepository _gameRepository;
    private int _id;
    private string _title = string.Empty;
    private string _developer = string.Empty;
    private string _platform = "PC";
    private GameStatus _status = GameStatus.Backlog;
    private double _rating;
    private int _hoursPlayed;
    private string _notes = string.Empty;
    private string? _coverUrl;
    private bool _isFavorite;
    private bool _isLiked;
    private bool _isMastered;

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
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsStatusBacklog));
                OnPropertyChanged(nameof(IsStatusPlaying));
                OnPropertyChanged(nameof(IsStatusPlayed));
                OnPropertyChanged(nameof(IsStatusCompleted));
                OnPropertyChanged(nameof(IsStatusAbandoned));
            }
        }
    }

    public bool IsStatusBacklog => Status == GameStatus.Backlog;
    public bool IsStatusPlaying => Status == GameStatus.Playing;
    public bool IsStatusPlayed => Status == GameStatus.Played;
    public bool IsStatusCompleted => Status == GameStatus.Completed;
    public bool IsStatusAbandoned => Status == GameStatus.Abandoned;

    /// <summary>0.0 = not rated, otherwise 0.5 .. 5.0 in steps of 0.5.</summary>
    public double Rating
    {
        get => _rating;
        set => SetProperty(ref _rating, NormalizeRating(value));
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

    public string? CoverUrl
    {
        get => _coverUrl;
        set
        {
            if (SetProperty(ref _coverUrl, value))
            {
                OnPropertyChanged(nameof(CoverPreviewUrl));
                OnPropertyChanged(nameof(HasCoverPreview));
            }
        }
    }

    /// <summary>The cover URL only when it is a valid http(s) address, so partial typing never triggers image loads.</summary>
    public string? CoverPreviewUrl => TryNormalizeCoverUrl(CoverUrl, out string? url) ? url : null;

    public bool HasCoverPreview => CoverPreviewUrl != null;

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsLiked
    {
        get => _isLiked;
        set => SetProperty(ref _isLiked, value);
    }

    public bool IsMastered
    {
        get => _isMastered;
        set => SetProperty(ref _isMastered, value);
    }

    public IReadOnlyList<string> Platforms { get; } =
    [
        "PC", "Steam Deck", "PlayStation", "Xbox", "Nintendo Switch", "Mobile"
    ];

    public IReadOnlyList<GameStatus> Statuses { get; } = Enum.GetValues<GameStatus>();

    public ICommand SaveGameCommand { get; }
    public ICommand SetStatusCommand { get; }
    public ICommand DecreaseRatingCommand { get; }
    public ICommand IncreaseRatingCommand { get; }
    public ICommand ClearRatingCommand { get; }
    public ICommand PasteCoverCommand { get; }
    public ICommand ClearCoverCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameFormViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        SaveGameCommand = new Command(async () => await SaveGameAsync());
        SetStatusCommand = new Command<string>(name =>
        {
            if (Enum.TryParse(name, true, out GameStatus status))
            {
                Status = status;
            }
        });
        DecreaseRatingCommand = new Command(() => Rating -= RatingStep);
        IncreaseRatingCommand = new Command(() => Rating += RatingStep);
        ClearRatingCommand = new Command(() => Rating = 0);
        PasteCoverCommand = new Command(async () => await PasteCoverAsync());
        ClearCoverCommand = new Command(() => CoverUrl = null);
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
        CoverUrl = game.CoverUrl;
        IsFavorite = game.IsFavorite;
        IsLiked = game.IsLiked;
        IsMastered = game.IsMastered;
    }

    private async Task PasteCoverAsync()
    {
        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                await ShowAlertAsync("Буфер обміну", "У буфері обміну немає тексту з посиланням.");
                return;
            }

            CoverUrl = text.Trim();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Буфер обміну", $"Не вдалося прочитати буфер обміну: {exception.Message}");
        }
    }

    private async Task SaveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await ShowAlertAsync("Помилка", "Назва гри є обов'язковою.");
            return;
        }

        if (Rating < 0 || Rating > MaxRating)
        {
            await ShowAlertAsync("Помилка", "Рейтинг має бути в діапазоні від 0.5 до 5.0 (0 = без оцінки).");
            return;
        }

        if (HoursPlayed < 0)
        {
            await ShowAlertAsync("Помилка", "Кількість годин не може бути від'ємною.");
            return;
        }

        string? coverUrl = null;
        if (!string.IsNullOrWhiteSpace(CoverUrl))
        {
            if (!TryNormalizeCoverUrl(CoverUrl, out coverUrl))
            {
                await ShowAlertAsync("Помилка", "Cover Image URL має бути повним посиланням http(s)://…");
                return;
            }
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
            Notes = Notes.Trim(),
            CoverUrl = coverUrl,
            IsFavorite = IsFavorite,
            IsLiked = IsLiked,
            IsMastered = IsMastered
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

    private static double NormalizeRating(double value)
    {
        double clamped = Math.Clamp(value, 0d, MaxRating);
        return Math.Round(clamped / RatingStep, MidpointRounding.AwayFromZero) * RatingStep;
    }

    private static bool TryNormalizeCoverUrl(string? raw, out string? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string trimmed = raw.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            uri.Host.Contains('.'))
        {
            url = trimmed;
            return true;
        }

        return false;
    }

    private static async Task ShowAlertAsync(string title, string message)
    {
        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync(title, message, "OK");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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