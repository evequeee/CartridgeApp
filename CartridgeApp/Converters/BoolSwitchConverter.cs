using System.Globalization;

namespace CartridgeApp.Converters;

/// <summary>
/// Picks one of two values depending on a bool. ConverterParameter = "TrueValue|FalseValue".
/// For a string target the values are returned as text ("♥|♡").
/// For a Color target the values are treated as resource keys from Colors.xaml ("XmbLike|XmbMuted"),
/// so no colour is hardcoded in code.
/// </summary>
public class BoolSwitchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is true;
        string[] parts = (parameter as string ?? "|").Split('|', 2);
        string picked = flag ? parts[0] : (parts.Length > 1 ? parts[1] : string.Empty);

        if (targetType == typeof(Color) || typeof(Brush).IsAssignableFrom(targetType))
        {
            if (Application.Current?.Resources.TryGetValue(picked, out object? resource) == true)
            {
                if (resource is Color color)
                {
                    return targetType == typeof(Color) ? color : new SolidColorBrush(color);
                }

                if (resource is Brush brush)
                {
                    return brush;
                }
            }

            return targetType == typeof(Color) ? Colors.White : new SolidColorBrush(Colors.White);
        }

        return picked;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}