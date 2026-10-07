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

    public async Task<IReadOnlyList<Game>> GetGamesAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<Game>().ToListAsync();
    }

    public async Task AddGameAsync(Game game)
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        await connection.InsertAsync(game);
    }

    public async Task UpdateGameAsync(Game game)
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        await connection.UpdateAsync(game);
    }

    public async Task DeleteGameAsync(Game game)
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        await connection.DeleteAsync(game);
    }
}