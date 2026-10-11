using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MoveMouse.Converters;

/// <summary>
/// Colors a segmented-tab header: a subtle accent tint when the tab is the
/// active one, fully transparent otherwise. Kept as a soft tint (rather than
/// a solid fill) so the default theme-aware text color stays readable
/// without needing a matching Foreground swap.
/// </summary>
public sealed class BoolToTabBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromArgb(64, 0, 120, 212));
    private static readonly SolidColorBrush InactiveBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is bool isActive && isActive) ? ActiveBrush : InactiveBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
