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
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync();
    }
}
