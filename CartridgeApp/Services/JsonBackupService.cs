using System.Text.Json;

using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// Serializes and restores the game library using JSON.
/// </summary>
public class JsonBackupService : IBackupService
{
    private readonly IGameRepository _gameRepository;

    public JsonBackupService(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async Task<string> ExportAsync()
    {
        IReadOnlyList<Game> games = await _gameRepository.GetGamesAsync();
        return JsonSerializer.Serialize(games, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task<int> ImportAsync(string json)
    {
        List<Game>? importedGames = JsonSerializer.Deserialize<List<Game>>(json);
        if (importedGames == null)
        {
            return 0;
        }

        IReadOnlyList<Game> existingGames = await _gameRepository.GetGamesAsync();
        int addedCount = 0;

        foreach (Game importedGame in importedGames)
        {
            bool duplicate = existingGames.Any(existing =>
                string.Equals(existing.Title, importedGame.Title, StringComparison.OrdinalIgnoreCase) &&
                existing.Platform == importedGame.Platform &&
                existing.Status == importedGame.Status);

            if (duplicate)
            {
                continue;
            }

            importedGame.Id = 0;
            await _gameRepository.AddGameAsync(importedGame);
            addedCount++;
        }

        return addedCount;
    }
}
