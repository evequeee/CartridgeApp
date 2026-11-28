using CartridgeApp.ViewModels;

namespace CartridgeApp;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadGamesAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        // The content area is capped at 1480px, so wide windows use a five-column poster grid.
        int span = width >= 1000 ? 5
            : width >= 800 ? 4
            : width >= 600 ? 3
            : 2;

        PosterLayout.Span = span;
        _viewModel.GridSpan = span;
    }
}