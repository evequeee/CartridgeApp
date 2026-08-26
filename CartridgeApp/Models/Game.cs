namespace CartridgeApp.Models;

public enum GameStatus
{
    Backlog,
    Playing,
    Completed,
    Abandoned
}

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Developer { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";

    public GameStatus Status { get; set; } = GameStatus.Backlog;

    public int? Rating { get; set; }
    public bool IsFavorite { get; set; } = false;
}