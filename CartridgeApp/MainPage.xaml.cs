using CartridgeApp.ViewModels;

namespace CartridgeApp;

public partial class MainPage : ContentPage
{
    // Poster tile (216 wide) + its horizontal margin; used to pick the number of grid columns.
    private const double PosterCellWidth = 232;
    private const double MaxContentWidth = 1050;
    private const double HorizontalPadding = 48;

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

        // 3-4 poster columns on a normal window, fewer on a narrow one.
        double contentWidth = Math.Min(width, MaxContentWidth) - HorizontalPadding;
        int span = Math.Clamp((int)Math.Floor(contentWidth / PosterCellWidth), 2, 6);

        PosterLayout.Span = span;
        _viewModel.GridSpan = span;
    }
}