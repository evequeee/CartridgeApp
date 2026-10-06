using SQLite;

using CartridgeApp.Models;

namespace CartridgeApp.Services;

public class SqliteGameRepository : IGameRepository
{
    private SQLiteAsyncConnection? _database;

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_database != null)
        {
            return _database;
        }

        string databasePath = Path.Combine(FileSystem.AppDataDirectory, "local.db3");
        _database = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        await _database.CreateTableAsync<Game>();
        return _database;
    }

    public Task<IReadOnlyList<Game>> GetGamesAsync() => throw new NotImplementedException();

    public Task AddGameAsync(Game game) => throw new NotImplementedException();

    public Task UpdateGameAsync(Game game) => throw new NotImplementedException();

    public Task DeleteGameAsync(Game game) => throw new NotImplementedException();
}