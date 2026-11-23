using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Coordinates the main library, filtering, roulette, and game actions.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IGameRepository _gameRepository;
    private string _searchQuery = string.Empty;
    private SortOption _selectedSortOption = SortOption.ByTitle;
    private GameStatus? _selectedStatusFilter;
    private bool _isBusy;
    private bool _spinOnlyFavorites;

    public ObservableCollection<Game> Games { get; set; }
    public ObservableCollection<Game> FilteredGames { get; }
    public int BacklogCount => Games.Count(game => game.Status == GameStatus.Backlog);
    public ObservableCollection<SortOption> SortOptions { get; } = new()
    {
        SortOption.ByTitle,
        SortOption.ByStatus,
        SortOption.ByRating
    };
    public ObservableCollection<GameStatus> StatusOptions { get; } = new(Enum.GetValues<GameStatus>());

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value;
            OnPropertyChanged(nameof(SearchQuery));
            ApplyFilter();
        }
    }

    public SortOption SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (_selectedSortOption == value)
            {
                return;
            }

            _selectedSortOption = value;
            OnPropertyChanged(nameof(SelectedSortOption));
            ApplyFilter();
        }
    }

    public GameStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter == value)
            {
                return;
            }

            _selectedStatusFilter = value;
            OnPropertyChanged(nameof(SelectedStatusFilter));
            ApplyFilter();
        }
    }

    public bool SpinOnlyFavorites
    {
        get => _spinOnlyFavorites;
        set
        {
            if (_spinOnlyFavorites == value)
            {
                return;
            }

            _spinOnlyFavorites = value;
            OnPropertyChanged(nameof(SpinOnlyFavorites));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public ICommand SpinCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand RemoveGameCommand { get; private set; }
    public ICommand ToggleFavoriteCommand { get; private set; }
    public ICommand ClearAllCommand { get; private set; }
    public ICommand SortCommand { get; private set; }
    public ICommand ApplySortingCommand { get; private set; }
    public ICommand ResetFiltersCommand { get; private set; }
    public ICommand QuickChangeStatusCommand { get; private set; }
    public ICommand EditGameCommand { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        Games = new ObservableCollection<Game>();
        FilteredGames = new ObservableCollection<Game>();
        Games.CollectionChanged += (_, _) => OnPropertyChanged(nameof(BacklogCount));

        SpinCommand = new Command(async () => await SpinRevolver());
        AddGameCommand = new Command(async () => await AddNewGame());
        RemoveGameCommand = new Command<Game>(RemoveGame);
        ToggleFavoriteCommand = new Command<Game>(ToggleFavorite);
        ClearAllCommand = new Command(ClearAll);
        SortCommand = new Command(SortGames);
        ApplySortingCommand = new Command(ApplyFilter);
        ResetFiltersCommand = new Command(ResetFilters);
        QuickChangeStatusCommand = new Command<Game>(async game => await QuickChangeStatus(game));
        EditGameCommand = new Command<Game>(async game => await EditGame(game));
        _ = LoadGamesAsync();
    }

    public async Task LoadGamesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();
            IReadOnlyList<Game> games = await _gameRepository.GetGamesAsync();
            Games.Clear();
            foreach (Game game in games)
            {
                Games.Add(game);
            }

            ApplyFilter();
        }
        catch (Exception exception)
        {
            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Помилка PostgreSQL", $"Не вдалося завантажити ігри: {exception.Message}", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SpinRevolver()
    {
        var backlogGames = Games
            .Where(game => game.Status == GameStatus.Backlog)
            .Where(game => !SpinOnlyFavorites || game.IsFavorite)
            .ToList();

        if (backlogGames.Count == 0)
        {
            Page? emptyPage = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (emptyPage != null)
            {
                await emptyPage.DisplayAlertAsync("Помилка рулетки", "Беклог порожній або немає іграм, що відповідають фільтру.", "OK");
            }
            return;
        }

        var random = new Random();
        int winnerIndex = random.Next(backlogGames.Count);
        var winnerGame = backlogGames[winnerIndex];

        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        string action = await page.DisplayActionSheetAsync(
            "Roulette Result",
            "Закрити",
            "Start Playing",
            $"{winnerGame.Title}");

        if (action == "Start Playing")
        {
            winnerGame.Status = GameStatus.Playing;
            await _gameRepository.UpdateGameAsync(winnerGame);
            ApplyFilter();
            OnPropertyChanged(nameof(BacklogCount));
        }
    }

    private async Task AddNewGame()
    {
        await Shell.Current.GoToAsync("GameFormPage");
    }

    private async Task EditGame(Game game)
    {
        if (game == null)
        {
            return;
        }

        await Shell.Current.GoToAsync("GameFormPage", new Dictionary<string, object>
        {
            ["Game"] = game
        });
    }

    private void RemoveGame(Game gameToRemove)
    {
        if (gameToRemove != null && Games.Contains(gameToRemove))
        {
            Games.Remove(gameToRemove);
            ApplyFilter();
        }
    }

    private void ToggleFavorite(Game game)
    {
        if (game != null && Games.Contains(game))
        {
            int index = Games.IndexOf(game);
            game.IsFavorite = !game.IsFavorite;

            Games.RemoveAt(index);
            Games.Insert(index, game);
            ApplyFilter();
        }
    }

    private void ClearAll()
    {
        Games.Clear();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<Game> filteredGames = Games;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filteredGames = filteredGames.Where(game =>
                game.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedStatusFilter.HasValue)
        {
            filteredGames = filteredGames.Where(game => game.Status == SelectedStatusFilter.Value);
        }

        filteredGames = SelectedSortOption switch
        {
            SortOption.ByStatus => filteredGames.OrderBy(game => game.Status).ThenBy(game => game.Title),
            SortOption.ByRating => filteredGames.OrderByDescending(game => game.Rating ?? 0).ThenBy(game => game.Title),
            _ => filteredGames.OrderBy(game => game.Title)
        };

        FilteredGames.Clear();
        foreach (Game game in filteredGames)
        {
            FilteredGames.Add(game);
        }
    }

    private void SortGames()
    {
        ApplyFilter();
    }

    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        SelectedSortOption = SortOption.ByTitle;
        SelectedStatusFilter = null;
    }

    private async Task QuickChangeStatus(Game game)
    {
        if (game == null || !Games.Contains(game))
        {
            return;
        }

        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        string[] statusOptions = Enum.GetNames<GameStatus>();
        string selectedStatus = await page.DisplayActionSheetAsync(
            "Змінити статус",
            "Скасувати",
            null,
            statusOptions);

        if (Enum.TryParse(selectedStatus, out GameStatus status))
        {
            int index = Games.IndexOf(game);
            game.Status = status;
            Games.RemoveAt(index);
            Games.Insert(index, game);
            ApplyFilter();
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}