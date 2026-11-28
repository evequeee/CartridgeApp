using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Backloggd-style game page: hero banner, instant actions (rating, like, 100%, favorite)
/// and the chronological play diary.
/// </summary>
public class GameDetailViewModel : ViewModelBase
{
    private const double RatingStep = 0.5;
    private const double MaxRating = 5.0;

    private readonly IGameRepository _gameRepository;
    private Game _game = new();
    private bool _isBusy;

    public GameDetailViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;

        LogPlayCommand = new Command(async () => await LogPlayAsync());
        EditGameCommand = new Command(async () => await EditGameAsync());
        DeleteGameCommand = new Command(async () => await DeleteGameAsync());
        ToggleLikeCommand = new Command(async () => await UpdateGameAsync(game => game.IsLiked = !game.IsLiked));
        ToggleMasteredCommand = new Command(async () => await UpdateGameAsync(game => game.IsMastered = !game.IsMastered));
        ToggleFavoriteCommand = new Command(async () => await UpdateGameAsync(game => game.IsFavorite = !game.IsFavorite));
        IncreaseRatingCommand = new Command(async () => await UpdateGameAsync(game => game.Rating = StepRating(game.Rating, RatingStep)));
        DecreaseRatingCommand = new Command(async () => await UpdateGameAsync(game => game.Rating = StepRating(game.Rating, -RatingStep)));
        ClearRatingCommand = new Command(async () => await UpdateGameAsync(game => game.Rating = 0));
        QuickInfoCommand = new Command(async () => await ShowQuickInfoAsync());
        DeleteLogCommand = new Command<PlayLog>(async log => await DeleteLogAsync(log));
    }

    public ObservableCollection<PlayLog> Logs { get; } = new();

    public int GameId => _game.Id;
    public string Title => _game.Title;
    public string Developer => _game.Developer;
    public string Platform => _game.Platform;
    public GameStatus Status => _game.Status;
    public double Rating => _game.Rating;
    public int HoursPlayed => _game.HoursPlayed;
    public string Notes => _game.Notes;
    public string? CoverUrl => _game.CoverUrl;
    public bool HasCover => _game.HasCover;
    public bool IsFavorite => _game.IsFavorite;
    public bool IsLiked => _game.IsLiked;
    public bool IsMastered => _game.IsMastered;
    public bool HasNotes => !string.IsNullOrWhiteSpace(_game.Notes);

    public bool HasLogs => Logs.Count > 0;
    public bool HasNoLogs => Logs.Count == 0;
    public string LogSummary => $"{Logs.Count} entries · {Logs.Sum(log => log.HoursSession):0.#} h logged";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand LogPlayCommand { get; }
    public ICommand EditGameCommand { get; }
    public ICommand DeleteGameCommand { get; }
    public ICommand ToggleLikeCommand { get; }
    public ICommand ToggleMasteredCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand IncreaseRatingCommand { get; }
    public ICommand DecreaseRatingCommand { get; }
    public ICommand ClearRatingCommand { get; }
    public ICommand QuickInfoCommand { get; }
    public ICommand DeleteLogCommand { get; }

    /// <summary>Sets the game passed through Shell navigation. The page reloads fresh data in OnAppearing.</summary>
    public void LoadGame(Game game)
    {
        _game = game;
        NotifyGameChanged();
    }

    /// <summary>Reloads the game and its log from the database (after editing or logging a session).</summary>
    public async Task RefreshAsync()
    {
        if (_game.Id == 0 || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();

            IReadOnlyList<Game> games = await _gameRepository.GetGamesAsync();
            Game? latest = games.FirstOrDefault(game => game.Id == _game.Id);
            if (latest == null)
            {
                // The game was deleted elsewhere.
                await Shell.Current.GoToAsync("..");
                return;
            }

            _game = latest;

            IReadOnlyList<PlayLog> logs = await _gameRepository.GetPlayLogsForGameAsync(_game.Id);
            Logs.Clear();
            foreach (PlayLog log in logs)
            {
                Logs.Add(log);
            }

            NotifyGameChanged();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося завантажити гру: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LogPlayAsync()
    {
        if (_game.Id == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("LogEntryPage", new Dictionary<string, object>
        {
            ["Game"] = _game
        });
    }

    private async Task EditGameAsync()
    {
        if (_game.Id == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("GameFormPage", new Dictionary<string, object>
        {
            ["Game"] = _game
        });
    }

    private async Task DeleteGameAsync()
    {
        if (_game.Id == 0)
        {
            return;
        }

        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete Game",
            $"Delete \"{_game.Title}\" and its play diary entries?",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _gameRepository.DeleteGameAsync(_game);
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося видалити гру: {exception.Message}");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Applies a change, shows it immediately and saves it; on failure the previous values are restored.</summary>
    private async Task UpdateGameAsync(Action<Game> change)
    {
        if (_game.Id == 0)
        {
            return;
        }

        Game backup = Clone(_game);
        try
        {
            change(_game);
            NotifyGameChanged();
            await _gameRepository.UpdateGameAsync(_game);
        }
        catch (Exception exception)
        {
            _game = backup;
            NotifyGameChanged();
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти зміни: {exception.Message}");
        }
    }

    private async Task ShowQuickInfoAsync()
    {
        double loggedHours = Logs.Sum(log => log.HoursSession);
        int replays = Logs.Count(log => log.IsReplay);
        string first = Logs.Count > 0 ? Logs.Min(log => log.LoggedDate).ToString("dd.MM.yyyy") : "—";
        string last = Logs.Count > 0 ? Logs.Max(log => log.LoggedDate).ToString("dd.MM.yyyy") : "—";
        string average = Logs.Count > 0 ? $"{loggedHours / Logs.Count:0.#} год." : "—";
        string rating = _game.Rating > 0 ? $"{_game.Rating:0.0} / 5.0" : "без оцінки";

        string info =
            $"Платформа: {_game.Platform}\n" +
            $"Розробник: {_game.Developer}\n" +
            $"Статус: {_game.Status}\n" +
            $"Оцінка: {rating}\n" +
            $"Години в бібліотеці: {_game.HoursPlayed}\n" +
            $"\n" +
            $"Записів у щоденнику: {Logs.Count} (з них replay: {replays})\n" +
            $"Залоговано годин: {loggedHours:0.#}\n" +
            $"Середня сесія: {average}\n" +
            $"Перший запис: {first}\n" +
            $"Останній запис: {last}\n" +
            $"\n" +
            $"♥ Like: {(_game.IsLiked ? "так" : "ні")} · ★ Favorite: {(_game.IsFavorite ? "так" : "ні")} · 🏆 100%: {(_game.IsMastered ? "так" : "ні")}";

        await ShowAlertAsync(_game.Title, info);
    }

    private async Task DeleteLogAsync(PlayLog log)
    {
        if (log == null)
        {
            return;
        }

        bool confirmed = await ConfirmAsync(
            "Видалити запис?",
            $"Запис від {log.LoggedDate:dd.MM.yyyy} буде видалено з журналу.",
            "Видалити",
            "Скасувати");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _gameRepository.DeletePlayLogAsync(log);
            Logs.Remove(log);
            NotifyLogsChanged();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося видалити запис: {exception.Message}");
        }
    }

    private static double StepRating(double current, double delta)
    {
        double next = Math.Clamp(current + delta, 0d, MaxRating);
        return Math.Round(next / RatingStep, MidpointRounding.AwayFromZero) * RatingStep;
    }

    private static Game Clone(Game source)
    {
        return new Game
        {
            Id = source.Id,
            Title = source.Title,
            Developer = source.Developer,
            Platform = source.Platform,
            Status = source.Status,
            Rating = source.Rating,
            HoursPlayed = source.HoursPlayed,
            Notes = source.Notes,
            IsFavorite = source.IsFavorite,
            CoverUrl = source.CoverUrl,
            IsLiked = source.IsLiked,
            IsMastered = source.IsMastered
        };
    }

    private void NotifyGameChanged()
    {
        OnPropertyChanged(nameof(GameId));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Developer));
        OnPropertyChanged(nameof(Platform));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Rating));
        OnPropertyChanged(nameof(HoursPlayed));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(IsLiked));
        OnPropertyChanged(nameof(IsMastered));
        NotifyLogsChanged();
    }

    private void NotifyLogsChanged()
    {
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        OnPropertyChanged(nameof(LogSummary));
    }
}