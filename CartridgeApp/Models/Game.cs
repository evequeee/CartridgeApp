using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CartridgeApp.Models;

public enum GameStatus
{
    Backlog = 0,
    Playing = 1,
    Played = 4,
    Completed = 2,
    Abandoned = 3
}

public class Game
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Developer { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";

    public GameStatus Status { get; set; } = GameStatus.Backlog;

    /// <summary>
    /// Rating in stars: 0.0 = not rated, otherwise 0.5 .. 5.0 in steps of 0.5.
    /// </summary>
    public double Rating { get; set; }

    public int HoursPlayed { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsFavorite { get; set; } = false;

    /// <summary>Public URL of the box art (vertical cover). Optional.</summary>
    public string? CoverUrl { get; set; }

    /// <summary>Backloggd-style "like" – independent from <see cref="IsFavorite"/> (pin).</summary>
    public bool IsLiked { get; set; }

    /// <summary>Game is finished at 100% / platinum.</summary>
    public bool IsMastered { get; set; }

    [NotMapped]
    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);

    /// <summary>Play log entries (diary) for this game.</summary>
    [JsonIgnore]
    public List<PlayLog> PlayLogs { get; set; } = new();
}