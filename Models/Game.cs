namespace CartridgeApp.Models;

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Developer { get; set; } = string.Empty;

    public string Status { get; set; } = "Backlog";

    public int? Rating { get; set; }
}