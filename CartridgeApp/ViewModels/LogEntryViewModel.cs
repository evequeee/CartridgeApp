using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Form for creating one diary entry (play session) for a game.
/// </summary>
public class LogEntryViewModel : ViewModelBase
{
    private const double MaxSessionHours = 24;

    private readonly IGameRepository _gameRepository;
    private Game _game = new();
    private DateTime _loggedDate = DateTime.Today;
    private GameStatus _status = GameStatus.Playing;
    private double _hoursSession;
    private bool _isReplay;
    private string _reviewText = string.Empty;
    private bool _isSaving;
    private double _gameRating;

    public LogEntryViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        SaveCommand = new Command(async () => await SaveAsync());
        SetStatusCommand = new Command<string>(name =>
        {
            if (Enum.TryParse(name, true, out GameStatus status))
            {
                Status = status;
            }
        });
        IncreaseRatingCommand = new Command(() => GameRating += 0.5);
        DecreaseRatingCommand = new Command(() => GameRating -= 0.5);
        ClearRatingCommand = new Command(() => GameRating = 0);
        IncreaseHoursCommand = new Command(() => HoursSession += 0.5);
        DecreaseHoursCommand = new Command(() => HoursSession -= 0.5);
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public string GameTitle => _game.Title;
    public string Platform => _game.Platform;
    public string? CoverUrl => _game.CoverUrl;
    public bool HasCover => _game.HasCover;

    public IReadOnlyList<GameStatus> Statuses { get; } = Enum.GetValues<GameStatus>();

    public DateTime LoggedDate
    {
        get => _loggedDate;
        set => SetProperty(ref _loggedDate, value);
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

    /// <summary>The game's overall rating (0.0 = not rated), adjustable from the log form in steps of 0.5.</summary>
    public double GameRating
    {
        get => _gameRating;
        set => SetProperty(ref _gameRating, Math.Round(Math.Clamp(value, 0d, 5d) * 2d, MidpointRounding.AwayFromZero) / 2d);
    }

    /// <summary>Hours played in this session, step 0.5.</summary>
    public double HoursSession
    {
        get => _hoursSession;
        set => SetProperty(ref _hoursSession, Math.Round(Math.Clamp(value, 0, MaxSessionHours) * 2d, MidpointRounding.AwayFromZero) / 2d);
    }

    public bool IsReplay
    {
        get => _isReplay;
        set => SetProperty(ref _isReplay, value);
    }

    public string ReviewText
    {
        get => _reviewText;
        set => SetProperty(ref _reviewText, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand SetStatusCommand { get; }
    public ICommand IncreaseRatingCommand { get; }
    public ICommand DecreaseRatingCommand { get; }
    public ICommand ClearRatingCommand { get; }
    public ICommand IncreaseHoursCommand { get; }
    public ICommand DecreaseHoursCommand { get; }
    public ICommand CancelCommand { get; }

    public void LoadGame(Game game)
    {
        _game = game;
        GameRating = game.Rating;
        Status = game.Status;
        // Playing an already finished game again is most likely a replay.
        IsReplay = game.Status is GameStatus.Played or GameStatus.Completed;

        OnPropertyChanged(nameof(GameTitle));
        OnPropertyChanged(nameof(Platform));
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCover));
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        if (_game.Id == 0)
        {
            await ShowAlertAsync("Помилка", "Гру не вибрано.");
            return;
        }

        if (HoursSession < 0 || HoursSession > MaxSessionHours)
        {
            await ShowAlertAsync("Помилка", "Години за сесію мають бути в діапазоні від 0 до 24.");
            return;
        }

        _isSaving = true;
        try
        {
            var log = new PlayLog
            {
                GameId = _game.Id,
                LoggedDate = LoggedDate.Date,
                HoursSession = HoursSession,
                StatusAfterSession = Status,
                ReviewText = string.IsNullOrWhiteSpace(ReviewText) ? null : ReviewText.Trim(),
                IsReplay = IsReplay
            };

            await _gameRepository.AddPlayLogAsync(log);

            // Keep the game card in sync with the diary: new status and the total hours (whole hours, rounded up from .5).
            _game.Status = Status;
            _game.Rating = GameRating;
            _game.HoursPlayed += (int)Math.Round(HoursSession, MidpointRounding.AwayFromZero);
            await _gameRepository.UpdateGameAsync(_game);

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти запис: {exception.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }
}