using CartridgeApp.Models;
using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

public partial class GameFormPage : ContentPage, IQueryAttributable
{
    private readonly GameFormViewModel _viewModel;

    public GameFormPage(GameFormViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Game", out object? value) && value is Game game)
        {
            _viewModel.LoadGame(game);
        }
    }
}
