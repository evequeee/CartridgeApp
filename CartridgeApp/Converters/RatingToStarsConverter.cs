using System.Globalization;

namespace CartridgeApp.Converters;

public class RatingToStarsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int rating || rating <= 0)
        {
            return "No rating";
        }

        return new string('★', Math.Clamp(rating, 1, 10));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
