using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CartridgeApp.Models;

/// <summary>
/// A single diary entry: one play session / review of a game (Backloggd-style log).
/// </summary>
public class PlayLog
{
    public int Id { get; set; }

    public int GameId { get; set; }

    [JsonIgnore]
    public Game Game { get; set; } = null!;

    public DateTime LoggedDate { get; set; } = DateTime.Today;

    public double HoursSession { get; set; }

    public GameStatus StatusAfterSession { get; set; } = GameStatus.Playing;

    public string? ReviewText { get; set; }

    public bool IsReplay { get; set; }

    [NotMapped]
    [JsonIgnore]
    public bool HasReview => !string.IsNullOrWhiteSpace(ReviewText);
}