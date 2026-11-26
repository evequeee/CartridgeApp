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
            Rating = 4.0
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
    public async Task NewGameFieldsArePersisted()
    {
        await using CartridgeDbContext context = CreateContext();
        var repository = new PostgresGameRepository(context);

        await repository.AddGameAsync(new Game
        {
            Title = "Hades",
            Developer = "Supergiant",
            Platform = "PC",
            Rating = 4.5,
            IsLiked = true,
            IsMastered = true,
            CoverUrl = "https://example.com/hades.jpg"
        });

        Game saved = Assert.Single(await repository.GetGamesAsync());

        Assert.Equal(4.5, saved.Rating);
        Assert.True(saved.IsLiked);
        Assert.True(saved.IsMastered);
        Assert.True(saved.HasCover);
        Assert.Equal("https://example.com/hades.jpg", saved.CoverUrl);
    }

    [Fact]
    public async Task PlayLogsAreAddedOrderedAndDeleted()
    {
        await using CartridgeDbContext context = CreateContext();
        var repository = new PostgresGameRepository(context);

        var game = new Game { Title = "Persona 4 Golden", Developer = "Atlus", Platform = "PC" };
        await repository.AddGameAsync(game);

        var older = new PlayLog
        {
            GameId = game.Id,
            LoggedDate = new DateTime(2026, 1, 10),
            HoursSession = 2.5,
            StatusAfterSession = GameStatus.Playing
        };
        var newer = new PlayLog
        {
            GameId = game.Id,
            LoggedDate = new DateTime(2026, 2, 20),
            HoursSession = 6,
            StatusAfterSession = GameStatus.Completed,
            ReviewText = "Great ending",
            IsReplay = true
        };

        await repository.AddPlayLogAsync(older);
        await repository.AddPlayLogAsync(newer);

        Assert.True(older.Id > 0);
        Assert.True(newer.Id > 0);

        IReadOnlyList<PlayLog> forGame = await repository.GetPlayLogsForGameAsync(game.Id);
        Assert.Equal(2, forGame.Count);
        Assert.Equal(newer.Id, forGame[0].Id);
        Assert.True(forGame[0].IsReplay);
        Assert.True(forGame[0].HasReview);

        IReadOnlyList<PlayLog> recent = await repository.GetRecentLogsAsync(1);
        PlayLog latest = Assert.Single(recent);
        Assert.Equal(newer.Id, latest.Id);
        Assert.Equal("Persona 4 Golden", latest.Game.Title);

        await repository.DeletePlayLogAsync(newer);
        PlayLog remaining = Assert.Single(await repository.GetPlayLogsForGameAsync(game.Id));
        Assert.Equal(older.Id, remaining.Id);
    }

    [Fact]
    public async Task UserProfileIsCreatedOnFirstAccessAndUpdatedInPlace()
    {
        await using CartridgeDbContext context = CreateContext();
        var repository = new PostgresGameRepository(context);

        UserProfile created = await repository.GetUserProfileAsync();
        Assert.True(created.Id > 0);
        Assert.Equal("Player One", created.DisplayName);
        Assert.StartsWith("@", created.Handle);

        created.DisplayName = "Test Hero";
        created.Handle = "@test_hero";
        created.Bio = "Bio";
        created.FavoritePlatform = "PS5";
        created.ShowcaseGameIdsCsv = "3,0,1,0";
        await repository.UpdateUserProfileAsync(created);

        UserProfile loaded = await repository.GetUserProfileAsync();
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal("Test Hero", loaded.DisplayName);
        Assert.Equal("@test_hero", loaded.Handle);
        Assert.Equal("3,0,1,0", loaded.ShowcaseGameIdsCsv);

        // There is only ever one profile row.
        Assert.Equal(1, await context.UserProfiles.CountAsync());

        // Saving a brand-new instance (Id 0) must update the existing row, not insert a second one.
        await repository.UpdateUserProfileAsync(new UserProfile { DisplayName = "Replaced", Handle = "@replaced" });
        Assert.Equal(1, await context.UserProfiles.CountAsync());
        Assert.Equal("Replaced", (await repository.GetUserProfileAsync()).DisplayName);
    }

    [Fact]
    public void DbContextConfiguresGamePrimaryKey()
    {
        using CartridgeDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model.FindEntityType(typeof(Game))!;

        Assert.NotNull(entity.FindPrimaryKey());
        Assert.Contains(entity.FindPrimaryKey()!.Properties, property => property.Name == "Id");
    }

    [Fact]
    public void DbContextConfiguresPlayLogRelationship()
    {
        using CartridgeDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model.FindEntityType(typeof(PlayLog))!;

        Assert.NotNull(entity.FindPrimaryKey());
        Microsoft.EntityFrameworkCore.Metadata.IForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Game), foreignKey.PrincipalEntityType.ClrType);
        Assert.Contains(foreignKey.Properties, property => property.Name == nameof(PlayLog.GameId));
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static CartridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CartridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CartridgeDbContext(options);
    }
}