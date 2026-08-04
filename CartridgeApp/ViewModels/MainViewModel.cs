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

    public MainViewModel()
    {
        _gameService = new MockGameService();
        Games = new ObservableCollection<Game>(_gameService.GetGames());

        SpinCommand = new Command(SpinRevolver);
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
}