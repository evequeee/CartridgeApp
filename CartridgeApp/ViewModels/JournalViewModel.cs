using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>Filter chips of the journal feed.</summary>
public enum JournalFilter
{
    All,
    Replays,
    Playing,
    Played,
    Completed,
    Backlog,
    Abandoned
}

/// <summary>
/// Global activity feed: all logged sessions of all games, newest first (like a Backloggd feed).
/// </summary>
public class JournalViewModel : ViewModelBase
{
    private const int FeedSize = 200;

    private readonly IGameRepository _gameRepository;
    private List<PlayLog> _allLogs = new();
    private JournalFilter _activeFilter = JournalFilter.All;
    private bool _isBusy;

    public JournalViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        RefreshCommand = new Command(async () => await LoadAsync());
        OpenGameCommand = new Command<Game>(async game => await OpenGameAsync(game));
        SetFilterCommand = new Command<string>(name =>
            ActiveFilter = Enum.TryParse(name, true, out JournalFilter filter) ? filter : JournalFilter.All);
    }

    /// <summary>Entries that pass the active filter (what the feed shows).</summary>
    public ObservableCollection<PlayLog> Logs { get; } = new();

    public bool HasLogs => Logs.Count > 0;

    /// <summary>"N entries · H h" for the entries currently shown.</summary>
    public string Summary => $"{Logs.Count} entries · {Logs.Sum(log => log.HoursSession):0.#} h";

    // Totals for the header cards: they describe the whole journal and do not jump when a filter is applied.
    public string SessionsText => _allLogs.Count.ToString();
    public string HoursText => $"{_allLogs.Sum(log => log.HoursSession):0.#}";
    public string ReplaysText => _allLogs.Count(log => log.IsReplay).ToString();
    public string ShowingText => $"Показано {Logs.Count} з {_allLogs.Count}";

    public JournalFilter ActiveFilter
    {
        get => _activeFilter;
        set
        {
            if (_activeFilter == value)
            {
                return;
            }

            _activeFilter = value;
            OnPropertyChanged(nameof(ActiveFilter));
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterReplays));
            OnPropertyChanged(nameof(IsFilterPlaying));
            OnPropertyChanged(nameof(IsFilterPlayed));
            OnPropertyChanged(nameof(IsFilterCompleted));
            OnPropertyChanged(nameof(IsFilterBacklog));
            OnPropertyChanged(nameof(IsFilterAbandoned));
            ApplyFilter();
        }
    }

    public bool IsFilterAll => ActiveFilter == JournalFilter.All;
    public bool IsFilterReplays => ActiveFilter == JournalFilter.Replays;
    public bool IsFilterPlaying => ActiveFilter == JournalFilter.Playing;
    public bool IsFilterPlayed => ActiveFilter == JournalFilter.Played;
    public bool IsFilterCompleted => ActiveFilter == JournalFilter.Completed;
    public bool IsFilterBacklog => ActiveFilter == JournalFilter.Backlog;
    public bool IsFilterAbandoned => ActiveFilter == JournalFilter.Abandoned;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenGameCommand { get; }
    public ICommand SetFilterCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();
            _allLogs = (await _gameRepository.GetRecentLogsAsync(FeedSize)).ToList();

            ApplyFilter();
            OnPropertyChanged(nameof(SessionsText));
            OnPropertyChanged(nameof(HoursText));
            OnPropertyChanged(nameof(ReplaysText));
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося завантажити журнал: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<PlayLog> filtered = ActiveFilter switch
        {
            JournalFilter.Replays => _allLogs.Where(log => log.IsReplay),
            JournalFilter.Playing => _allLogs.Where(log => log.StatusAfterSession == GameStatus.Playing),
            JournalFilter.Played => _allLogs.Where(log => log.StatusAfterSession == GameStatus.Played),
            JournalFilter.Completed => _allLogs.Where(log => log.StatusAfterSession == GameStatus.Completed),
            JournalFilter.Backlog => _allLogs.Where(log => log.StatusAfterSession == GameStatus.Backlog),
            JournalFilter.Abandoned => _allLogs.Where(log => log.StatusAfterSession == GameStatus.Abandoned),
            _ => _allLogs
        };

        Logs.Clear();
        foreach (PlayLog log in filtered)
        {
            Logs.Add(log);
        }

        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ShowingText));
    }

    private static async Task OpenGameAsync(Game game)
    {
        if (game == null || game.Id == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("GameDetailPage", new Dictionary<string, object>
        {
            ["Game"] = game
        });
    }
}