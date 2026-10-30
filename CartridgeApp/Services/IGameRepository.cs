using CartridgeApp.Models;

namespace CartridgeApp.Services;

public interface IGameRepository
{
    Task<IReadOnlyList<Game>> GetGamesAsync();
    Task AddGameAsync(Game game);
    Task UpdateGameAsync(Game game);
    Task DeleteGameAsync(Game game);
    Task<BacklogStats> GetStatisticsAsync();
}