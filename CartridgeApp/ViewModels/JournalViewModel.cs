using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Global activity feed: all logged sessions of all games, newest first (like a Backloggd feed).
/// </summary>
public class JournalViewModel : ViewModelBase
{
    private const int FeedSize = 200;

    private readonly IGameRepository _gameRepository;
    private bool _isBusy;

    public JournalViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        RefreshCommand = new Command(async () => await RefreshAsync());
        OpenGameCommand = new Command<Game>(async game => await OpenGameAsync(game));
    }

    public ObservableCollection<PlayLog> Logs { get; } = new();

    public bool HasLogs => Logs.Count > 0;
    public string Summary => $"{Logs.Count} entries · {Logs.Sum(log => log.HoursSession):0.#} h";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenGameCommand { get; }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();
            IReadOnlyList<PlayLog> logs = await _gameRepository.GetRecentLogsAsync(FeedSize);

            Logs.Clear();
            foreach (PlayLog log in logs)
            {
                Logs.Add(log);
            }

            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(Summary));
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