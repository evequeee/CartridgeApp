using CartridgeApp.Models;
using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

public partial class LogEntryPage : ContentPage, IQueryAttributable
{
    private readonly LogEntryViewModel _viewModel;

    public LogEntryPage(LogEntryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Game", out object? value) && value is Game game)
        {
            _viewModel.LoadGame(game);
        }
    }
}
