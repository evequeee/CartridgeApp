using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

public class MainViewModel
{
    private readonly MockGameService _gameService;
    public ObservableCollection<Game> Games { get; set; }

    public ICommand SpinCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand AddGameCommand { get; private set; }
    public ICommand RemoveGameCommand { get; private set; }

    public MainViewModel()
    {
        _gameService = new MockGameService();
        Games = new ObservableCollection<Game>(_gameService.GetGames());

        SpinCommand = new Command(SpinRevolver);
        AddGameCommand = new Command(AddNewGame);

        RemoveGameCommand = new Command<Game>(RemoveGame);
    }

    private void SpinRevolver()
    {
        if (Games == null || Games.Count == 0)
        {
            Application.Current.MainPage.DisplayAlert("Oops", "Барабан пустий! Додай ігри.", "OK");
            return;
        }

        var random = new Random();
        int winnerIndex = random.Next(Games.Count);
        var winnerGame = Games[winnerIndex];

        Application.Current.MainPage.DisplayAlert(
            "Roulette Result",
            $"Твоя наступна гра:\n{winnerGame.Title}",
            "Пішов грати!"
        );
    }

    private void AddNewGame()
    {
        Games.Add(new Game
        {
            Title = "Terraria",
            Developer = "Re-Logic",
            Status = GameStatus.Playing
        });
    }

    private void RemoveGame(Game gameToRemove)
    {
        if (gameToRemove != null && Games.Contains(gameToRemove))
        {
            Games.Remove(gameToRemove);
        }
    }
}