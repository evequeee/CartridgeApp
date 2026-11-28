namespace CartridgeApp.Models;

/// <summary>
/// Player rank, derived from the number of completed games. Shared by the profile page and the top bar.
/// </summary>
public static class PlayerRank
{
    private static readonly (int Threshold, string Name)[] Tiers =
    [
        (0, "ROOKIE"),
        (2, "CHALLENGER"),
        (5, "VETERAN"),
        (10, "ELITE"),
        (20, "MASTER"),
        (40, "LEGEND")
    ];

    public static (string Rank, string Details) Resolve(int completed)
    {
        int current = 0;
        for (int i = 0; i < Tiers.Length; i++)
        {
            if (completed >= Tiers[i].Threshold)
            {
                current = i;
            }
        }

        string details = current == Tiers.Length - 1
            ? $"{completed} completed · max rank"
            : $"{completed} completed · {Tiers[current + 1].Threshold - completed} to {Tiers[current + 1].Name}";

        return (Tiers[current].Name, details);
    }
}