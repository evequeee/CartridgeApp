using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>Quick filter chips under the search bar.</summary>
public enum QuickFilter
{
    All,
    Backlog,
    Playing,
    Played,
    Completed,
    Abandoned,
    Liked,
    Mastered
}

/// <summary>
/// Coordinates the main library, filtering, roulette, and game actions.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IGameRepository _gameRepository;
    private readonly Random _random = new();
    private string _searchQuery = string.Empty;
    private SortOption _selectedSortOption = SortOption.ByTitle;
    private GameStatus? _selectedStatusFilter;
    private QuickFilter _activeQuickFilter = QuickFilter.All;
    private bool _isBusy;
    private bool _spinOnlyFavorites;
    private bool _showLikedOnly;
    private bool _isGridView = true;
    private int _gridSpan = 4;
    private bool _isRouletteOpen;
    private Game? _rouletteGame;

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

    public QuickFilter ActiveQuickFilter
    {
        get => _activeQuickFilter;
        set
        {
            if (_activeQuickFilter == value)
            {
                return;
            }

            _activeQuickFilter = value;
            OnPropertyChanged(nameof(ActiveQuickFilter));
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterBacklog));
            OnPropertyChanged(nameof(IsFilterPlaying));
            OnPropertyChanged(nameof(IsFilterPlayed));
            OnPropertyChanged(nameof(IsFilterCompleted));
            OnPropertyChanged(nameof(IsFilterAbandoned));
            OnPropertyChanged(nameof(IsFilterLiked));
            OnPropertyChanged(nameof(IsFilterMastered));
            ApplyFilter();
        }
    }

    public bool IsFilterAll => ActiveQuickFilter == QuickFilter.All;
    public bool IsFilterBacklog => ActiveQuickFilter == QuickFilter.Backlog;
    public bool IsFilterPlaying => ActiveQuickFilter == QuickFilter.Playing;
    public bool IsFilterPlayed => ActiveQuickFilter == QuickFilter.Played;
    public bool IsFilterCompleted => ActiveQuickFilter == QuickFilter.Completed;
    public bool IsFilterAbandoned => ActiveQuickFilter == QuickFilter.Abandoned;
    public bool IsFilterLiked => ActiveQuickFilter == QuickFilter.Liked;
    public bool IsFilterMastered => ActiveQuickFilter == QuickFilter.Mastered;

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

    /// <summary>Extra "liked only" switch (kept for compatibility; the ♥ Liked chip does the same job).</summary>
    public bool ShowLikedOnly
    {
        get => _showLikedOnly;
        set
        {
            if (_showLikedOnly == value)
            {
                return;
            }

            _showLikedOnly = value;
            OnPropertyChanged(nameof(ShowLikedOnly));
            ApplyFilter();
        }
    }

    /// <summary>True = poster grid, false = compact detailed list.</summary>
    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            if (_isGridView == value)
            {
                return;
            }

            _isGridView = value;
            OnPropertyChanged(nameof(IsGridView));
            OnPropertyChanged(nameof(IsListView));
        }
    }

    public bool IsListView => !IsGridView;

    /// <summary>Number of poster columns; the page adapts it to the window width.</summary>
    public int GridSpan
    {
        get => _gridSpan;
        set
        {
            int span = Math.Clamp(value, 2, 5);
            if (_gridSpan == span)
            {
                return;
            }

            _gridSpan = span;
            OnPropertyChanged(nameof(GridSpan));
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

    // ----- roulette overlay state
    public bool IsRouletteOpen
    {
        get => _isRouletteOpen;
        private set
        {
            if (_isRouletteOpen == value)
            {
                return;
            }

            _isRouletteOpen = value;
            OnPropertyChanged(nameof(IsRouletteOpen));
        }
    }

    public string RouletteTitle => _rouletteGame?.Title ?? string.Empty;
    public string RouletteDeveloper => _rouletteGame?.Developer ?? string.Empty;
    public string RoulettePlatform => _rouletteGame?.Platform ?? string.Empty;
    public string RouletteHoursText => $"{_rouletteGame?.HoursPlayed ?? 0} год. зіграно";
    public string? RouletteCoverUrl => _rouletteGame?.CoverUrl;
    public bool RouletteHasCover => _rouletteGame?.HasCover ?? false;
    public string RouletteCandidatesText => $"{BacklogCandidates().Count} у барабані";

    public ICommand SpinCommand { get; private set; }
    public ICommand RerollCommand { get; private set; }
    public ICommand StartPlayingCommand { get; private set; }
    public ICommand OpenRouletteGameCommand { get; private set; }
    public ICommand CloseRouletteCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand RemoveGameCommand { get; private set; }
    public ICommand ToggleFavoriteCommand { get; private set; }
    public ICommand ToggleLikeCommand { get; private set; }
    public ICommand ClearAllCommand { get; private set; }
    public ICommand SortCommand { get; private set; }
    public ICommand ApplySortingCommand { get; private set; }
    public ICommand ResetFiltersCommand { get; private set; }
    public ICommand QuickChangeStatusCommand { get; private set; }
    public ICommand EditGameCommand { get; private set; }
    public ICommand OpenGameDetailCommand { get; private set; }
    public ICommand SetQuickFilterCommand { get; private set; }
    public ICommand SetViewModeCommand { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        Games = new ObservableCollection<Game>();
        FilteredGames = new ObservableCollection<Game>();
        Games.CollectionChanged += (_, _) => OnPropertyChanged(nameof(BacklogCount));

        SpinCommand = new Command(async () => await SpinRevolver());
        RerollCommand = new Command(() => Reroll());
        StartPlayingCommand = new Command(async () => await StartPlayingRouletteGame());
        OpenRouletteGameCommand = new Command(async () => await OpenRouletteGame());
        CloseRouletteCommand = new Command(() => IsRouletteOpen = false);
        AddGameCommand = new Command(async () => await AddNewGame());
        RemoveGameCommand = new Command<Game>(async game => await RemoveGameAsync(game));
        ToggleFavoriteCommand = new Command<Game>(async game => await ToggleFavoriteAsync(game));
        ToggleLikeCommand = new Command<Game>(async game => await ToggleLikeAsync(game));
        ClearAllCommand = new Command(ClearAll);
        SortCommand = new Command(SortGames);
        ApplySortingCommand = new Command(ApplyFilter);
        ResetFiltersCommand = new Command(ResetFilters);
        QuickChangeStatusCommand = new Command<Game>(async game => await QuickChangeStatus(game));
        EditGameCommand = new Command<Game>(async game => await EditGame(game));
        OpenGameDetailCommand = new Command<Game>(async game => await OpenGameDetail(game));
        SetQuickFilterCommand = new Command<string>(SetQuickFilter);
        SetViewModeCommand = new Command<string>(mode => IsGridView = !string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase));
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

    private void SetQuickFilter(string? name)
    {
        ActiveQuickFilter = Enum.TryParse(name, true, out QuickFilter filter) ? filter : QuickFilter.All;
    }

    // ------------------------------------------------------------------ roulette

    private List<Game> BacklogCandidates()
    {
        return Games
            .Where(game => game.Status == GameStatus.Backlog)
            .Where(game => !SpinOnlyFavorites || game.IsFavorite)
            .ToList();
    }

    private async Task SpinRevolver()
    {
        List<Game> candidates = BacklogCandidates();

        if (candidates.Count == 0)
        {
            Page? emptyPage = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (emptyPage != null)
            {
                await emptyPage.DisplayAlertAsync("Помилка рулетки", "Беклог порожній або немає ігор, що відповідають фільтру.", "OK");
            }
            return;
        }

        SetRouletteGame(candidates[_random.Next(candidates.Count)]);
        IsRouletteOpen = true;
    }

    private void Reroll()
    {
        List<Game> candidates = BacklogCandidates();
        if (candidates.Count == 0)
        {
            IsRouletteOpen = false;
            return;
        }

        // Avoid showing the same game again when there is a choice.
        List<Game> pool = candidates.Count > 1 && _rouletteGame != null
            ? candidates.Where(game => game.Id != _rouletteGame.Id).ToList()
            : candidates;

        SetRouletteGame(pool[_random.Next(pool.Count)]);
    }

    private void SetRouletteGame(Game game)
    {
        _rouletteGame = game;
        OnPropertyChanged(nameof(RouletteTitle));
        OnPropertyChanged(nameof(RouletteDeveloper));
        OnPropertyChanged(nameof(RoulettePlatform));
        OnPropertyChanged(nameof(RouletteHoursText));
        OnPropertyChanged(nameof(RouletteCoverUrl));
        OnPropertyChanged(nameof(RouletteHasCover));
        OnPropertyChanged(nameof(RouletteCandidatesText));
    }

    private async Task StartPlayingRouletteGame()
    {
        if (_rouletteGame == null)
        {
            return;
        }

        Game winnerGame = _rouletteGame;
        winnerGame.Status = GameStatus.Playing;
        IsRouletteOpen = false;

        try
        {
            await _gameRepository.UpdateGameAsync(winnerGame);
        }
        catch (Exception exception)
        {
            winnerGame.Status = GameStatus.Backlog;
            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти статус: {exception.Message}", "OK");
            }
        }

        ReplaceInCollection(winnerGame);
        OnPropertyChanged(nameof(BacklogCount));
    }

    private async Task OpenRouletteGame()
    {
        Game? game = _rouletteGame;
        IsRouletteOpen = false;

        if (game != null)
        {
            await OpenGameDetail(game);
        }
    }

    // ------------------------------------------------------------------ navigation

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

    private async Task OpenGameDetail(Game game)
    {
        if (game == null)
        {
            return;
        }

        await Shell.Current.GoToAsync("GameDetailPage", new Dictionary<string, object>
        {
            ["Game"] = game
        });
    }

    // ------------------------------------------------------------------ list actions

    private async Task RemoveGameAsync(Game gameToRemove)
    {
        if (gameToRemove == null || !Games.Contains(gameToRemove))
        {
            return;
        }

        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        bool confirmed = await page.DisplayAlertAsync(
            "Видалити гру?",
            $"«{gameToRemove.Title}» та її записи щоденника буде видалено.",
            "Видалити",
            "Скасувати");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _gameRepository.DeleteGameAsync(gameToRemove);
            Games.Remove(gameToRemove);
            ApplyFilter();
        }
        catch (Exception exception)
        {
            await page.DisplayAlertAsync("Помилка PostgreSQL", $"Не вдалося видалити гру: {exception.Message}", "OK");
        }
    }

    private async Task ToggleFavoriteAsync(Game game)
    {
        if (game == null || !Games.Contains(game))
        {
            return;
        }

        game.IsFavorite = !game.IsFavorite;
        ReplaceInCollection(game);
        await PersistFlagAsync(game, () => game.IsFavorite = !game.IsFavorite);
    }

    private async Task ToggleLikeAsync(Game game)
    {
        if (game == null || !Games.Contains(game))
        {
            return;
        }

        game.IsLiked = !game.IsLiked;
        ReplaceInCollection(game);
        await PersistFlagAsync(game, () => game.IsLiked = !game.IsLiked);
    }

    /// <summary>Saves a toggled flag; if saving fails the flag is rolled back and the list refreshed.</summary>
    private async Task PersistFlagAsync(Game game, Action rollback)
    {
        try
        {
            await _gameRepository.UpdateGameAsync(game);
        }
        catch (Exception exception)
        {
            rollback();
            ReplaceInCollection(game);

            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти зміни: {exception.Message}", "OK");
            }
        }
    }

    // The cards are bound to plain POCOs, so a changed game is re-inserted at the same position to refresh its card.
    private void ReplaceInCollection(Game game)
    {
        int index = Games.IndexOf(game);
        if (index >= 0)
        {
            Games.RemoveAt(index);
            Games.Insert(index, game);
        }

        ApplyFilter();
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

        if (ShowLikedOnly)
        {
            filteredGames = filteredGames.Where(game => game.IsLiked);
        }

        filteredGames = ActiveQuickFilter switch
        {
            QuickFilter.Backlog => filteredGames.Where(game => game.Status == GameStatus.Backlog),
            QuickFilter.Playing => filteredGames.Where(game => game.Status == GameStatus.Playing),
            QuickFilter.Played => filteredGames.Where(game => game.Status == GameStatus.Played),
            QuickFilter.Completed => filteredGames.Where(game => game.Status == GameStatus.Completed),
            QuickFilter.Abandoned => filteredGames.Where(game => game.Status == GameStatus.Abandoned),
            QuickFilter.Liked => filteredGames.Where(game => game.IsLiked),
            QuickFilter.Mastered => filteredGames.Where(game => game.IsMastered),
            _ => filteredGames
        };

        filteredGames = SelectedSortOption switch
        {
            SortOption.ByStatus => filteredGames.OrderBy(game => game.Status).ThenBy(game => game.Title),
            SortOption.ByRating => filteredGames.OrderByDescending(game => game.Rating).ThenBy(game => game.Title),
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
        ShowLikedOnly = false;
        ActiveQuickFilter = QuickFilter.All;
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