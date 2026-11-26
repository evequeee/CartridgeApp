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
            Rating = 5.0,
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
        // Re-importing the same file must not duplicate play logs either.
        Assert.Equal(5, (await repository.GetRecentLogsAsync(int.MaxValue)).Count);
    }

    [Fact]
    public async Task JsonBackupIncludesPlayLogsAndRestoresThemIntoEmptyLibrary()
    {
        var source = new MockGameService();
        string json = await new JsonBackupService(source).ExportAsync();

        Assert.Contains("PlayLogs", json);
        Assert.Contains("Profile", json);
        Assert.Contains("Фінал і True Ending пройдені.", json);

        var target = new MockGameService();
        foreach (Game game in await target.GetGamesAsync())
        {
            await target.DeleteGameAsync(game);
        }

        Assert.Empty(await target.GetRecentLogsAsync(int.MaxValue));

        int added = await new JsonBackupService(target).ImportAsync(json);

        Assert.Equal(6, added);
        Assert.Equal(5, (await target.GetRecentLogsAsync(int.MaxValue)).Count);
    }

    [Fact]
    public async Task JsonBackupRestoresProfileAndRelinksShowcaseByTitle()
    {
        var source = new MockGameService();
        await source.UpdateUserProfileAsync(new UserProfile
        {
            DisplayName = "Test Hero",
            Handle = "@test_hero",
            Bio = "Hello",
            FavoritePlatform = "PS5",
            // Baldur's Gate 3, empty, Persona 4 Golden, empty (ids are those of MockGameService)
            ShowcaseGameIdsCsv = "5,0,1,0"
        });

        string json = await new JsonBackupService(source).ExportAsync();

        var target = new MockGameService();
        foreach (Game game in await target.GetGamesAsync())
        {
            await target.DeleteGameAsync(game);
        }

        await new JsonBackupService(target).ImportAsync(json);

        UserProfile restored = await target.GetUserProfileAsync();
        Assert.Equal("Test Hero", restored.DisplayName);
        Assert.Equal("@test_hero", restored.Handle);
        Assert.Equal("PS5", restored.FavoritePlatform);

        int[] slots = restored.ShowcaseGameIdsCsv.Split(',').Select(int.Parse).ToArray();
        Assert.Equal(4, slots.Length);

        IReadOnlyList<Game> games = await target.GetGamesAsync();
        // Ids differ in the new library; the slots must still point at the same titles.
        Assert.Equal("Baldur's Gate 3", games.Single(game => game.Id == slots[0]).Title);
        Assert.Equal(0, slots[1]);
        Assert.Equal("Persona 4 Golden", games.Single(game => game.Id == slots[2]).Title);
        Assert.Equal(0, slots[3]);
    }

    [Fact]
    public async Task LegacyBackupWithIntegerRatingsIsConvertedToStars()
    {
        const string legacyJson = """
            [
              { "Id": 1, "Title": "Old Game", "Developer": "X", "Platform": "PC", "Status": 2, "Rating": 9, "HoursPlayed": 3, "Notes": "", "IsFavorite": true },
              { "Id": 2, "Title": "Unrated Game", "Developer": "Y", "Platform": "PC", "Status": 0, "Rating": null, "HoursPlayed": 0, "Notes": "", "IsFavorite": false }
            ]
            """;

        var repository = new MockGameService();
        int added = await new JsonBackupService(repository).ImportAsync(legacyJson);

        Assert.Equal(2, added);

        IReadOnlyList<Game> games = await repository.GetGamesAsync();
        Assert.Equal(4.5, games.Single(game => game.Title == "Old Game").Rating);
        Assert.Equal(0.0, games.Single(game => game.Title == "Unrated Game").Rating);
    }

    [Fact]
    public void SearchStatusAndRatingSortingStayCorrect()
    {
        var games = new MockGameService().GetGames();

        var filtered = games
            .Where(game => game.Title.Contains("slay", StringComparison.OrdinalIgnoreCase))
            .Where(game => game.Status == GameStatus.Playing)
            .OrderByDescending(game => game.Rating)
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Slay the Spire", filtered[0].Title);

        var sorted = games.OrderByDescending(game => game.Rating).ToList();
        Assert.Equal("Persona 4 Golden", sorted[0].Title);
    }

    [Fact]
    public void QuickFilterEquivalentsSelectTheRightGames()
    {
        var games = new MockGameService().GetGames();

        var liked = games.Where(game => game.IsLiked).Select(game => game.Title).OrderBy(title => title).ToList();
        Assert.Equal(new[] { "Hades", "Persona 4 Golden", "Slay the Spire" }, liked);

        var mastered = games.Where(game => game.IsMastered).Select(game => game.Title).ToList();
        Assert.Equal(new[] { "Persona 4 Golden" }, mastered);

        var playing = games.Where(game => game.Status == GameStatus.Playing).Select(game => game.Title).OrderBy(title => title).ToList();
        Assert.Equal(new[] { "Baldur's Gate 3", "Slay the Spire" }, playing);

        var completed = games.Where(game => game.Status == GameStatus.Completed).Select(game => game.Title).OrderBy(title => title).ToList();
        Assert.Equal(new[] { "Hades", "Persona 4 Golden" }, completed);
    }

    [Fact]
    public void SeedGamesUseDirectSteamPosterUrls()
    {
        var games = new MockGameService().GetGames();

        Assert.All(games, game =>
        {
            Assert.True(game.HasCover);
            Assert.StartsWith("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/", game.CoverUrl);
            Assert.EndsWith("/library_600x900.jpg", game.CoverUrl);
        });

        Assert.Contains("/apps/1091500/", games.Single(game => game.Title == "Cyberpunk 2077").CoverUrl);
        Assert.Contains("/apps/1113000/", games.Single(game => game.Title == "Persona 4 Golden").CoverUrl);
        Assert.Contains("/apps/646570/", games.Single(game => game.Title == "Slay the Spire").CoverUrl);
        Assert.Contains("/apps/1145360/", games.Single(game => game.Title == "Hades").CoverUrl);
        Assert.Contains("/apps/1086940/", games.Single(game => game.Title == "Baldur's Gate 3").CoverUrl);
        Assert.Contains("/apps/1245620/", games.Single(game => game.Title == "Elden Ring").CoverUrl);
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
        Assert.True(await context.PlayLogs.AnyAsync());
        Assert.True(await context.UserProfiles.AnyAsync());

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

        IReadOnlyList<string> backlog = games
            .Where(game => game.Status == GameStatus.Backlog)
            .Select(game => game.Title)
            .OrderBy(title => title)
            .ToList();

        Assert.Equal(new[] { "Cyberpunk 2077", "Elden Ring" }, backlog);
    }

    private static CartridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CartridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CartridgeDbContext(options);
    }
}