using Microsoft.EntityFrameworkCore;

using CartridgeApp.Data;
using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.Tests;

public class RepositoryTests
{
    [Fact]
    public async Task CrudOperationsPersistAndReturnGames()
    {
        await using CartridgeDbContext context = CreateContext();
        var repository = new PostgresGameRepository(context);

        var game = new Game
        {
            Title = "Test Game",
            Developer = "Test Studio",
            Platform = "PC",
            Status = GameStatus.Backlog,
            Rating = 8
        };

        await repository.AddGameAsync(game);
        IReadOnlyList<Game> games = await repository.GetGamesAsync();

        Assert.Contains(games, item => item.Title == "Test Game");

        game.Status = GameStatus.Playing;
        await repository.UpdateGameAsync(game);
        await repository.DeleteGameAsync(game);

        Assert.Empty(await repository.GetGamesAsync());
    }

    [Fact]
    public void DbContextConfiguresGamePrimaryKey()
    {
        using CartridgeDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model.FindEntityType(typeof(Game))!;

        Assert.NotNull(entity.FindPrimaryKey());
        Assert.True(entity.FindPrimaryKey()!.Properties.Any(property => property.Name == "Id"));
    }

    private static CartridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CartridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CartridgeDbContext(options);
    }
}
