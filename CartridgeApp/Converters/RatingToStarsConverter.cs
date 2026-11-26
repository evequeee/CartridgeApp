using System.Globalization;

namespace CartridgeApp.Converters;

/// <summary>
/// Converts a 0.0..5.0 rating (step 0.5) to a visual string.
/// Default: "★★★★½". With ConverterParameter="badge": "★ 4.5 / 5.0". Rating 0 = "No rating".
/// </summary>
public class RatingToStarsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double rating = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            decimal m => (double)m,
            _ => 0d
        };

        rating = Math.Round(Math.Clamp(rating, 0d, 5d) * 2d, MidpointRounding.AwayFromZero) / 2d;

        if (rating <= 0d)
        {
            return "No rating";
        }

        if (parameter is string mode && mode.Equals("badge", StringComparison.OrdinalIgnoreCase))
        {
            return string.Create(CultureInfo.InvariantCulture, $"★ {rating:0.0} / 5.0");
        }

        int fullStars = (int)Math.Floor(rating);
        bool hasHalf = rating - fullStars >= 0.5d;

        return new string('★', fullStars) + (hasHalf ? "½" : string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}