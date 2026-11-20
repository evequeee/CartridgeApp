using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// Provides asynchronous persistence operations for the game library.
/// </summary>
public interface IGameRepository
{
    /// <summary>Gets all games from persistent storage.</summary>
    Task<IReadOnlyList<Game>> GetGamesAsync();
    Task AddGameAsync(Game game);
    Task UpdateGameAsync(Game game);
    Task DeleteGameAsync(Game game);
    Task<BacklogStats> GetStatisticsAsync();
}