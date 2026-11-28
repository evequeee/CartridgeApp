using CartridgeApp.Models;
using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

public partial class GameDetailPage : ContentPage, IQueryAttributable
{
    private readonly GameDetailViewModel _viewModel;

    public GameDetailPage(GameDetailViewModel viewModel)
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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}