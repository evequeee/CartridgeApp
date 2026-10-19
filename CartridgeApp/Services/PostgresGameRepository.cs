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
