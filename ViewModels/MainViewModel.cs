using System.Collections.ObjectModel;
using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

public class MainViewModel
{
    private readonly MockGameService _gameService;

    public ObservableCollection<Game> Games { get; set; }

    public MainViewModel()
    {
        _gameService = new MockGameService();

        Games = new ObservableCollection<Game>(_gameService.GetGames());
    }
}