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
        // TODO
    }
}