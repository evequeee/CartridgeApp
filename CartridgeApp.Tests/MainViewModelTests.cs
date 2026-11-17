using CartridgeApp.Models;
using CartridgeApp.Services;
using CartridgeApp.ViewModels;

namespace CartridgeApp.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task SearchAndStatusFilterReturnMatchingGames()
    {
        var viewModel = new MainViewModel(new MockGameService());
        await viewModel.LoadGamesAsync();

        viewModel.SearchQuery = "slay";
        viewModel.SelectedStatusFilter = GameStatus.Playing;

        Assert.Single(viewModel.FilteredGames);
        Assert.Equal("Slay the Spire", viewModel.FilteredGames[0].Title);
    }

    [Fact]
    public async Task RatingSortingShowsHighestRatingFirst()
    {
        var viewModel = new MainViewModel(new MockGameService());
        await viewModel.LoadGamesAsync();

        viewModel.SelectedSortOption = SortOption.ByRating;

        Assert.Equal("Persona 4 Golden", viewModel.FilteredGames[0].Title);
    }

    [Fact]
    public async Task ToggleFavoriteAndFavoritesRouletteOptionWork()
    {
        var viewModel = new MainViewModel(new MockGameService());
        await viewModel.LoadGamesAsync();
        Game cyberpunk = viewModel.Games.Single(game => game.Title == "Cyberpunk 2077");

        viewModel.ToggleFavoriteCommand.Execute(cyberpunk);
        viewModel.SpinOnlyFavorites = true;

        Assert.True(cyberpunk.IsFavorite);
        Assert.True(viewModel.SpinOnlyFavorites);
    }
}
