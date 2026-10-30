namespace CartridgeApp.Services;

using CartridgeApp.Models;

public class MockGameService : IGameRepository
{
    private readonly List<Game> _games =
    [
        new Game
        {
            Title = "Persona 4 Golden",
            Developer = "Atlus",
            Platform = "PC",
            Status = GameStatus.Completed,
            Rating = 10,
            IsFavorite = true
        },
        new Game
        {
            Title = "Slay the Spire",
            Developer = "Mega Crit",
            Platform = "Steam Deck",
            Status = GameStatus.Playing,
            Rating = 9,
            IsFavorite = true
        },
        new Game
        {
            Title = "Cyberpunk 2077",
            Developer = "CD Projekt Red",
            Platform = "PC",
            Status = GameStatus.Backlog,
            IsFavorite = true
        }
    ];

    public List<Game> GetGames()
    {
        return _games.ToList();
    }

    public Task<IReadOnlyList<Game>> GetGamesAsync()
    {
        return Task.FromResult<IReadOnlyList<Game>>(_games.ToList());
    }

    public Task AddGameAsync(Game game)
    {
        _games.Add(game);
        return Task.CompletedTask;
    }

    public Task UpdateGameAsync(Game game)
    {
        int index = _games.FindIndex(existing => existing.Id == game.Id);
        if (index >= 0)
        {
            _games[index] = game;
        }

        return Task.CompletedTask;
    }

    public Task DeleteGameAsync(Game game)
    {
        _games.RemoveAll(existing => existing.Id == game.Id);
        return Task.CompletedTask;
    }

    public Task<BacklogStats> GetStatisticsAsync()
    {
        int total = _games.Count;
        int completed = _games.Count(game => game.Status == GameStatus.Completed);
        var stats = new BacklogStats
        {
            TotalGames = total,
            CompletedPercentage = total == 0 ? 0 : completed * 100d / total,
            TotalHoursPlayed = _games.Sum(game => game.HoursPlayed),
            FavoriteGames = _games.Count(game => game.IsFavorite),
            GamesByPlatform = _games
                .GroupBy(game => game.Platform)
                .ToDictionary(group => group.Key, group => group.Count())
        };

        return Task.FromResult(stats);
    }
}