namespace CartridgeApp.Models;

public sealed class AchievementBadge
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required bool IsUnlocked { get; init; }
    public required Color AccentColor { get; init; }
    public required int GridColumn { get; init; }
    public required int GridRow { get; init; }
}
