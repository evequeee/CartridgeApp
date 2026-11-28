namespace CartridgeApp.Models;

/// <summary>
/// The (single) local player profile shown on the Profile tab.
/// </summary>
public class UserProfile
{
    public int Id { get; set; }

    public string DisplayName { get; set; } = "Player One";

    /// <summary>Public handle, always stored with a leading '@'.</summary>
    public string Handle { get; set; } = "@cartridge_hunter";

    public string Bio { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string FavoritePlatform { get; set; } = "PC / PS5";

    /// <summary>
    /// Four pinned game ids for the showcase, comma separated, 0 = empty slot (for example "12,0,7,3").
    /// </summary>
    public string ShowcaseGameIdsCsv { get; set; } = "0,0,0,0";

    public int? CrownedGameId { get; set; }
}