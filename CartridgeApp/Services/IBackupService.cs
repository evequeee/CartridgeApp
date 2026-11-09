namespace CartridgeApp.Services;

/// <summary>
/// Provides JSON export and import operations for the game library.
/// </summary>
public interface IBackupService
{
    Task<string> ExportAsync();
    Task<int> ImportAsync(string json);
}
