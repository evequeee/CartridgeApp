using Microsoft.EntityFrameworkCore;

using CartridgeApp.Data;
using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.Tests;

public class RuntimeTests
{
    [Fact]
    public async Task EFRepositorySupportsCrudAndStatistics()
    {
        await using var context = CreateContext();
        var repository = new PostgresGameRepository(context);

        await repository.AddGameAsync(new Game
        {
            Title = "Hades",
            Developer = "Supergiant",
            Platform = "PC",
            Status = GameStatus.Completed,
            Rating = 10,
            HoursPlayed = 30,
            IsFavorite = true
        });

        IReadOnlyList<Game> games = await repository.GetGamesAsync();
        Assert.Single(games);

        BacklogStats stats = await repository.GetStatisticsAsync();
        Assert.Equal(1, stats.TotalGames);
        Assert.Equal(100d, stats.CompletedPercentage);
        Assert.Equal(30, stats.TotalHoursPlayed);
        Assert.Equal(1, stats.FavoriteGames);

        await repository.DeleteGameAsync(games[0]);
        Assert.Empty(await repository.GetGamesAsync());
    }

    [Fact]
    public async Task JsonBackupExportsAndRejectsDuplicates()
    {
        var repository = new MockGameService();
        var backup = new JsonBackupService(repository);

        string json = await backup.ExportAsync();

        Assert.Contains("Persona 4 Golden", json);
        Assert.Equal(0, await backup.ImportAsync(json));
    }

    [Fact]
    public void SearchStatusAndRatingSortingStayCorrect()
    {
        var games = new MockGameService().GetGames();

        var filtered = games
            .Where(game => game.Title.Contains("slay", StringComparison.OrdinalIgnoreCase))
            .Where(game => game.Status == GameStatus.Playing)
            .OrderByDescending(game => game.Rating ?? 0)
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Slay the Spire", filtered[0].Title);

        var sorted = games.OrderByDescending(game => game.Rating ?? 0).ToList();
        Assert.Equal("Persona 4 Golden", sorted[0].Title);
    }

    [Fact]
    public async Task LivePostgresDatabaseCanEnsureSchemaAndSeed()
    {
        var options = new DbContextOptionsBuilder<CartridgeDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cartridge;Username=postgres;Password=cartridge")
            .Options;

        await using var context = new CartridgeDbContext(options);
        var repository = new PostgresGameRepository(context);

        await repository.EnsureDatabaseAsync();

        Assert.True(await context.Database.CanConnectAsync());
        Assert.True(await context.Games.AnyAsync());

        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT current_database(), inet_server_addr(), inet_server_port(), to_regclass('public.\"Games\"')::text, (SELECT count(*) FROM \"Games\")";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("cartridge", reader.GetString(0));
        Console.WriteLine($"server={reader[1]} port={reader[2]} table={reader[3]} count={reader[4]}");
        Assert.True(reader.GetInt64(4) >= 3);
    }

    [Fact]
    public void RouletteCandidatesRespectBacklogAndFavorites()
    {
        var games = new MockGameService().GetGames();

        IReadOnlyList<Game> backlogFavorites = games
            .Where(game => game.Status == GameStatus.Backlog)
            .Where(game => game.IsFavorite)
            .ToList();

        Assert.Single(backlogFavorites);
        Assert.Equal("Cyberpunk 2077", backlogFavorites[0].Title);

        IReadOnlyList<Game> backlog = games
            .Where(game => game.Status == GameStatus.Backlog)
            .ToList();

        Assert.Single(backlog);
        Assert.Equal("Cyberpunk 2077", backlog[0].Title);
    }

    private static CartridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CartridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CartridgeDbContext(options);
    }
}
