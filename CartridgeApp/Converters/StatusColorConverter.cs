using System.Globalization;

using CartridgeApp.Models;

namespace CartridgeApp.Converters;

/// <summary>
/// Maps a <see cref="GameStatus"/> to its neon XMB colour.
/// Used for the status stripe on the card and the status badge text.
/// </summary>
public class StatusColorConverter : IValueConverter
{
    private static readonly Color CompletedColor = Color.FromArgb("#00E676"); // green
    private static readonly Color PlayingColor = Color.FromArgb("#FF4081");   // pink-magenta
    private static readonly Color BacklogColor = Color.FromArgb("#00B4FF");   // light blue
    private static readonly Color AbandonedColor = Color.FromArgb("#FF5252"); // red
    private static readonly Color DefaultColor = Color.FromArgb("#233B6E");   // XMB border blue

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GameStatus status)
        {
            return status switch
            {
                GameStatus.Completed => CompletedColor,
                GameStatus.Playing => PlayingColor,
                GameStatus.Backlog => BacklogColor,
                GameStatus.Abandoned => AbandonedColor,
                _ => DefaultColor
            };
        }

        return Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}