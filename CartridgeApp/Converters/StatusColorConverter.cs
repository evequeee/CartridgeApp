using System.Globalization;

using CartridgeApp.Models;

namespace CartridgeApp.Converters;

public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GameStatus status)
        {
            return status switch
            {
                GameStatus.Completed => Colors.LightGreen,
                GameStatus.Playing => Colors.LightSkyBlue,
                GameStatus.Backlog => Colors.LightGray,
                GameStatus.Abandoned => Colors.IndianRed,
                _ => Colors.Transparent
            };
        }

        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}