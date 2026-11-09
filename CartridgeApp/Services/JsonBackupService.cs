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

    public Task<string> ExportAsync() => throw new NotImplementedException();

    public Task<int> ImportAsync(string json) => throw new NotImplementedException();
}
