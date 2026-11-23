using System.Globalization;

using CartridgeApp.Models;

namespace CartridgeApp.Converters;

public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GameStatus status)
        {
            return status switch
            {
                GameStatus.Completed => Color.FromArgb("#00E676"),
                GameStatus.Playing => Color.FromArgb("#FF4081"),
                GameStatus.Backlog => Color.FromArgb("#00B0FF"),
                GameStatus.Abandoned => Color.FromArgb("#FF5252"),
                _ => Color.FromArgb("#264785")
            };
        }

        return Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}