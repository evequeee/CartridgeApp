namespace CartridgeApp.Models;

/// <summary>
/// Aggregated statistics about the game library.
/// </summary>
public class BacklogStats
{
    public int TotalGames { get; set; }
    public double CompletedPercentage { get; set; }
    public int TotalHoursPlayed { get; set; }
    public int FavoriteGames { get; set; }
    public Dictionary<string, int> GamesByPlatform { get; set; } = new();
}
