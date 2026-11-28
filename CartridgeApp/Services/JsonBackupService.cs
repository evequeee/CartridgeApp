using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// Serializes and restores the game library (including play logs and the player profile) using JSON.
/// </summary>
/// <remarks>
/// Format v2 is an object { Version, Games, PlayLogs, Profile }. The old format (a bare JSON array of games
/// with a 1..10 integer rating) is still accepted on import and converted to the 0.5..5.0 scale.
/// Play logs and the profile showcase reference games by title + platform, not by database id,
/// so a backup can be restored into a different database.
/// </remarks>
public class JsonBackupService : IBackupService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Keep Cyrillic reviews/notes readable in the file instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IGameRepository _gameRepository;

    public JsonBackupService(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async Task<string> ExportAsync()
    {
        IReadOnlyList<Game> games = await _gameRepository.GetGamesAsync();
        IReadOnlyList<PlayLog> logs = await _gameRepository.GetRecentLogsAsync(int.MaxValue);
        UserProfile profile = await _gameRepository.GetUserProfileAsync();

        var showcase = new List<GameRef>();
        foreach (int id in ParseShowcaseIds(profile.ShowcaseGameIdsCsv))
        {
            Game? pinned = games.FirstOrDefault(game => game.Id == id);
            showcase.Add(pinned == null
                ? new GameRef()
                : new GameRef { Title = pinned.Title, Platform = pinned.Platform });
        }
        Game? crownedGame = profile.CrownedGameId is int crownedId
            ? games.FirstOrDefault(game => game.Id == crownedId)
            : null;

        var package = new BackupPackage
        {
            Version = 2,
            ExportedAtUtc = DateTime.UtcNow,
            Games = games.ToList(),
            PlayLogs = logs
                .Where(log => log.Game != null)
                .Select(log => new PlayLogBackup
                {
                    GameTitle = log.Game.Title,
                    GamePlatform = log.Game.Platform,
                    LoggedDate = log.LoggedDate,
                    HoursSession = log.HoursSession,
                    StatusAfterSession = log.StatusAfterSession,
                    ReviewText = log.ReviewText,
                    IsReplay = log.IsReplay
                })
                .ToList(),
            Profile = new ProfileBackup
            {
                DisplayName = profile.DisplayName,
                Handle = profile.Handle,
                Bio = profile.Bio,
                AvatarUrl = profile.AvatarUrl,
                FavoritePlatform = profile.FavoritePlatform,
                ShowcaseGames = showcase,
                CrownedGameId = profile.CrownedGameId,
                CrownedGame = crownedGame == null
                    ? null
                    : new GameRef { Title = crownedGame.Title, Platform = crownedGame.Platform }
            }
        };

        return JsonSerializer.Serialize(package, WriteOptions);
    }

    /// <summary>Imports games, play logs and the profile. Returns the number of newly added games.</summary>
    public async Task<int> ImportAsync(string json)
    {
        ParsedBackup parsed = Parse(json);

        IReadOnlyList<Game> existingGames = await _gameRepository.GetGamesAsync();
        var gameIdsByKey = new Dictionary<string, int>();
        foreach (Game existing in existingGames)
        {
            gameIdsByKey.TryAdd(GameKey(existing.Title, existing.Platform), existing.Id);
        }

        int addedCount = 0;

        foreach (Game importedGame in parsed.Games)
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
            importedGame.Rating = NormalizeRating(importedGame.Rating);
            await _gameRepository.AddGameAsync(importedGame);
            gameIdsByKey.TryAdd(GameKey(importedGame.Title, importedGame.Platform), importedGame.Id);
            addedCount++;
        }

        if (parsed.Logs.Count > 0)
        {
            IReadOnlyList<PlayLog> existingLogs = await _gameRepository.GetRecentLogsAsync(int.MaxValue);
            var knownSignatures = new HashSet<string>(
                existingLogs.Select(log => Signature(log.GameId, log.LoggedDate, log.HoursSession,
                    log.StatusAfterSession, log.IsReplay, log.ReviewText)));

            foreach (PlayLogBackup backup in parsed.Logs)
            {
                if (!gameIdsByKey.TryGetValue(GameKey(backup.GameTitle, backup.GamePlatform), out int gameId))
                {
                    continue;
                }

                string signature = Signature(gameId, backup.LoggedDate, backup.HoursSession,
                    backup.StatusAfterSession, backup.IsReplay, backup.ReviewText);

                if (!knownSignatures.Add(signature))
                {
                    continue;
                }

                await _gameRepository.AddPlayLogAsync(new PlayLog
                {
                    GameId = gameId,
                    LoggedDate = backup.LoggedDate,
                    HoursSession = backup.HoursSession,
                    StatusAfterSession = backup.StatusAfterSession,
                    ReviewText = backup.ReviewText,
                    IsReplay = backup.IsReplay
                });
            }
        }

        if (parsed.Profile != null)
        {
            // The backup is the user's own export, so its profile replaces the current one.
            // Showcase slots are re-linked to games of this database by title + platform.
            var slotIds = new List<int>();
            for (int slot = 0; slot < 4; slot++)
            {
                GameRef? reference = slot < parsed.Profile.ShowcaseGames.Count ? parsed.Profile.ShowcaseGames[slot] : null;
                slotIds.Add(reference != null &&
                            !string.IsNullOrWhiteSpace(reference.Title) &&
                            gameIdsByKey.TryGetValue(GameKey(reference.Title, reference.Platform), out int pinnedId)
                    ? pinnedId
                    : 0);
            }

            int? crownedGameId = null;
            GameRef? crownedReference = parsed.Profile.CrownedGame;
            if (crownedReference != null &&
                !string.IsNullOrWhiteSpace(crownedReference.Title) &&
                gameIdsByKey.TryGetValue(GameKey(crownedReference.Title, crownedReference.Platform), out int remappedCrownedId))
            {
                crownedGameId = remappedCrownedId;
            }
            else if (parsed.Profile.CrownedGameId is int oldCrownedId)
            {
                Game? originalCrownedGame = parsed.Games.FirstOrDefault(game => game.Id == oldCrownedId);
                if (originalCrownedGame != null &&
                    gameIdsByKey.TryGetValue(GameKey(originalCrownedGame.Title, originalCrownedGame.Platform), out int fallbackCrownedId))
                {
                    crownedGameId = fallbackCrownedId;
                }
            }

            await _gameRepository.UpdateUserProfileAsync(new UserProfile
            {
                DisplayName = string.IsNullOrWhiteSpace(parsed.Profile.DisplayName) ? "Player One" : parsed.Profile.DisplayName,
                Handle = string.IsNullOrWhiteSpace(parsed.Profile.Handle) ? "@cartridge_hunter" : parsed.Profile.Handle,
                Bio = parsed.Profile.Bio ?? string.Empty,
                AvatarUrl = parsed.Profile.AvatarUrl,
                FavoritePlatform = string.IsNullOrWhiteSpace(parsed.Profile.FavoritePlatform) ? "PC / PS5" : parsed.Profile.FavoritePlatform,
                ShowcaseGameIdsCsv = string.Join(",", slotIds),
                CrownedGameId = crownedGameId
            });
        }

        return addedCount;
    }

    private static ParsedBackup Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            // Legacy v1: bare array, Rating was a nullable integer 1..10.
            List<LegacyGame>? legacy = JsonSerializer.Deserialize<List<LegacyGame>>(json, ReadOptions);
            List<Game> games = (legacy ?? new List<LegacyGame>())
                .Select(item => new Game
                {
                    Title = item.Title ?? string.Empty,
                    Developer = item.Developer ?? string.Empty,
                    Platform = string.IsNullOrWhiteSpace(item.Platform) ? "PC" : item.Platform,
                    Status = item.Status,
                    Rating = NormalizeRating((item.Rating ?? 0) / 2d),
                    HoursPlayed = item.HoursPlayed,
                    Notes = item.Notes ?? string.Empty,
                    IsFavorite = item.IsFavorite
                })
                .ToList();

            return new ParsedBackup(games, new List<PlayLogBackup>(), null);
        }

        BackupPackage? package = JsonSerializer.Deserialize<BackupPackage>(json, ReadOptions);
        return new ParsedBackup(
            package?.Games ?? new List<Game>(),
            package?.PlayLogs ?? new List<PlayLogBackup>(),
            package?.Profile);
    }

    private static IEnumerable<int> ParseShowcaseIds(string? csv)
    {
        string[] parts = (csv ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        for (int slot = 0; slot < 4; slot++)
        {
            yield return slot < parts.Length && int.TryParse(parts[slot], out int id) ? id : 0;
        }
    }

    private static double NormalizeRating(double rating)
    {
        double clamped = Math.Clamp(rating, 0d, 5d);
        return Math.Round(clamped * 2d, MidpointRounding.AwayFromZero) / 2d;
    }

    private static string GameKey(string title, string platform)
    {
        return $"{title.Trim().ToLowerInvariant()}|{platform.Trim().ToLowerInvariant()}";
    }

    private static string Signature(int gameId, DateTime date, double hours, GameStatus status, bool replay, string? review)
    {
        return $"{gameId}|{date.Ticks}|{hours:R}|{status}|{replay}|{(review ?? string.Empty).Trim()}";
    }

    private sealed record ParsedBackup(List<Game> Games, List<PlayLogBackup> Logs, ProfileBackup? Profile);

    private sealed class LegacyGame
    {
        public string? Title { get; set; }
        public string? Developer { get; set; }
        public string? Platform { get; set; }
        public GameStatus Status { get; set; }
        public int? Rating { get; set; }
        public int HoursPlayed { get; set; }
        public string? Notes { get; set; }
        public bool IsFavorite { get; set; }
    }
}

/// <summary>Root object of the v2 backup file.</summary>
public class BackupPackage
{
    public int Version { get; set; } = 2;
    public DateTime ExportedAtUtc { get; set; }
    public List<Game> Games { get; set; } = new();
    public List<PlayLogBackup> PlayLogs { get; set; } = new();
    public ProfileBackup? Profile { get; set; }
}

/// <summary>A play log in a backup; the game is referenced by title + platform instead of a database id.</summary>
public class PlayLogBackup
{
    public string GameTitle { get; set; } = string.Empty;
    public string GamePlatform { get; set; } = "PC";
    public DateTime LoggedDate { get; set; }
    public double HoursSession { get; set; }
    public GameStatus StatusAfterSession { get; set; }
    public string? ReviewText { get; set; }
    public bool IsReplay { get; set; }
}

/// <summary>The player profile in a backup. Showcase slots are game references (empty reference = empty slot).</summary>
public class ProfileBackup
{
    public string DisplayName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string FavoritePlatform { get; set; } = string.Empty;
    public List<GameRef> ShowcaseGames { get; set; } = new();
    public int? CrownedGameId { get; set; }
    public GameRef? CrownedGame { get; set; }
}

public class GameRef
{
    public string Title { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";
}