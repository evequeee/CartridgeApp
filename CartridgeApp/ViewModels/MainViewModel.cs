using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MockGameService _gameService;
    private string _searchQuery = string.Empty;
    private SortOption _selectedSortOption = SortOption.ByTitle;
    private GameStatus? _selectedStatusFilter;

    public ObservableCollection<Game> Games { get; set; }
    public ObservableCollection<Game> FilteredGames { get; }
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

    public ICommand SpinCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand RemoveGameCommand { get; private set; }
    public ICommand ToggleFavoriteCommand { get; private set; }
    public ICommand ClearAllCommand { get; private set; }
    public ICommand SortCommand { get; private set; }
    public ICommand ApplySortingCommand { get; private set; }
    public ICommand ResetFiltersCommand { get; private set; }
    public ICommand QuickChangeStatusCommand { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _gameService = new MockGameService();
        Games = new ObservableCollection<Game>(_gameService.GetGames());
        FilteredGames = new ObservableCollection<Game>(Games);
        ApplyFilter();

        SpinCommand = new Command(SpinRevolver);
        AddGameCommand = new Command(async () => await AddNewGame());
        RemoveGameCommand = new Command<Game>(RemoveGame);
        ToggleFavoriteCommand = new Command<Game>(ToggleFavorite);
        ClearAllCommand = new Command(ClearAll);
        SortCommand = new Command(SortGames);
        ApplySortingCommand = new Command(ApplyFilter);
        ResetFiltersCommand = new Command(ResetFilters);
        QuickChangeStatusCommand = new Command<Game>(async game => await QuickChangeStatus(game));
    }

    private void SpinRevolver()
    {
        var backlogGames = Games.Where(g => g.Status == GameStatus.Backlog).ToList();

        if (backlogGames.Count == 0)
        {
            Application.Current.MainPage.DisplayAlert("Oops", "Беклог пустий! Часом додати щось нове.", "OK");
            return;
        }

        var random = new Random();
        int winnerIndex = random.Next(backlogGames.Count);
        var winnerGame = backlogGames[winnerIndex];

        Application.Current.MainPage.DisplayAlert(
            "Roulette Result",
            $"Твоя наступна гра:\n{winnerGame.Title}",
            "Пішов грати!"
        );
    }

    private async Task AddNewGame()
    {
        string result = await Application.Current.MainPage.DisplayPromptAsync(
            "Додати в Cartridge",
            "Введи назву нової гри:"
        );

        if (!string.IsNullOrWhiteSpace(result))
        {
            Games.Add(new Game
            {
                Title = result,
                Developer = "Unknown",
                Status = GameStatus.Backlog
            });

            ApplyFilter();
        }
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

    private Task QuickChangeStatus(Game game)
    {
        return Task.CompletedTask;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}