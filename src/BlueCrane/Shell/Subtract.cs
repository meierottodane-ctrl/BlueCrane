using System.Globalization;
using System.Windows.Data;

namespace BlueCrane.Shell;

/// <summary>
/// Returns a width minus a fixed reserve, floored at zero.
///
/// Used to cap the tab strip so it stops short of the new-tab button and window
/// controls. Without a finite cap the strip would measure against infinity, tabs would
/// never shrink, and the button would be pushed to the far edge of the window.
/// </summary>
public sealed class Subtract : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width) return 0d;
        var reserve = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0d;
        return Math.Max(0d, width - reserve);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
