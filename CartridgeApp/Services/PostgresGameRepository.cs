using Microsoft.EntityFrameworkCore;

using CartridgeApp.Data;
using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// PostgreSQL-backed implementation of the game repository.
/// </summary>
public class PostgresGameRepository : IGameRepository
{
    private readonly CartridgeDbContext _dbContext;

    public PostgresGameRepository(CartridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureDatabaseAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        if (!await _dbContext.Games.AnyAsync())
        {
            _dbContext.Games.AddRange(
                new Game
                {
                    Title = "Persona 4 Golden",
                    Developer = "Atlus",
                    Platform = "PC",
                    Status = GameStatus.Completed,
                    Rating = 10,
                    HoursPlayed = 85,
                    IsFavorite = true
                },
                new Game
                {
                    Title = "Slay the Spire",
                    Developer = "Mega Crit",
                    Platform = "Steam Deck",
                    Status = GameStatus.Playing,
                    Rating = 9,
                    HoursPlayed = 42,
                    IsFavorite = true
                },
                new Game
                {
                    Title = "Cyberpunk 2077",
                    Developer = "CD Projekt Red",
                    Platform = "PC",
                    Status = GameStatus.Backlog,
                    HoursPlayed = 0
                });

            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync()
    {
        return await _dbContext.Games
            .AsNoTracking()
            .OrderBy(game => game.Title)
            .ToListAsync();
    }

    public async Task AddGameAsync(Game game)
    {
        await _dbContext.Games.AddAsync(game);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateGameAsync(Game game)
    {
        _dbContext.Games.Update(game);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteGameAsync(Game game)
    {
        Game? trackedGame = await _dbContext.Games.FindAsync(game.Id);
        if (trackedGame != null)
        {
            _dbContext.Games.Remove(trackedGame);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<BacklogStats> GetStatisticsAsync()
    {
        IReadOnlyList<Game> games = await _dbContext.Games.AsNoTracking().ToListAsync();
        int total = games.Count;
        int completed = games.Count(game => game.Status == GameStatus.Completed);

        return new BacklogStats
        {
            TotalGames = total,
            CompletedPercentage = total == 0 ? 0 : completed * 100d / total,
            TotalHoursPlayed = games.Sum(game => game.HoursPlayed),
            FavoriteGames = games.Count(game => game.IsFavorite),
            GamesByPlatform = games
                .GroupBy(game => game.Platform)
                .ToDictionary(group => group.Key, group => group.Count())
        };
    }
}
