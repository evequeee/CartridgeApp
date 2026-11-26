using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// Provides asynchronous persistence operations for the game library and play logs.
/// </summary>
public interface IGameRepository
{
    /// <summary>Gets all games from persistent storage.</summary>
    Task<IReadOnlyList<Game>> GetGamesAsync();
    Task AddGameAsync(Game game);
    Task UpdateGameAsync(Game game);
    Task DeleteGameAsync(Game game);
    Task<BacklogStats> GetStatisticsAsync();
    Task EnsureDatabaseAsync();

    /// <summary>Adds a diary entry. <see cref="PlayLog.GameId"/> must be set.</summary>
    Task AddPlayLogAsync(PlayLog log);

    /// <summary>All diary entries of one game, newest first.</summary>
    Task<IReadOnlyList<PlayLog>> GetPlayLogsForGameAsync(int gameId);

    /// <summary>Latest diary entries across all games (with <see cref="PlayLog.Game"/> loaded), newest first.</summary>
    Task<IReadOnlyList<PlayLog>> GetRecentLogsAsync(int take = 100);

    Task DeletePlayLogAsync(PlayLog log);

    /// <summary>Gets the player profile; a default one is created on first access.</summary>
    Task<UserProfile> GetUserProfileAsync();

    /// <summary>Saves the player profile (there is only one).</summary>
    Task UpdateUserProfileAsync(UserProfile profile);
}