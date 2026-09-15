using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

public class MainViewModel
{
    private readonly MockGameService _gameService;
    public ObservableCollection<Game> Games { get; set; }
    public ICommand SpinCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand RemoveGameCommand { get; private set; }
    public ICommand ToggleFavoriteCommand { get; private set; }
    public ICommand ClearAllCommand { get; private set; }
    public MainViewModel()
    {
        _gameService = new MockGameService();
        Games = new ObservableCollection<Game>(_gameService.GetGames());

        SpinCommand = new Command(SpinRevolver);
        AddGameCommand = new Command(async () => await AddNewGame());
        RemoveGameCommand = new Command<Game>(RemoveGame);
        ToggleFavoriteCommand = new Command<Game>(ToggleFavorite);
        ClearAllCommand = new Command(() => Games.Clear());
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
        }
    }

    private void RemoveGame(Game gameToRemove)
    {
        if (gameToRemove != null && Games.Contains(gameToRemove))
        {
            Games.Remove(gameToRemove);
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
        }
    }
}