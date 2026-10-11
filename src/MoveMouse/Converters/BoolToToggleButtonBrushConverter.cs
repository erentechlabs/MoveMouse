using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MoveMouse.Converters;

/// <summary>
/// Colors the single Start/Stop toggle button: accent blue when pressing it
/// would start jiggling, a warm red when pressing it would stop jiggling.
/// This keeps the button's call-to-action color in sync with what it will
/// do next, reinforcing the status dot and text elsewhere on the dashboard.
/// </summary>
public sealed class BoolToToggleButtonBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush StartBrush = new(Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush StopBrush = new(Color.FromArgb(255, 196, 43, 28));

    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is bool isRunning && isRunning) ? StopBrush : StartBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
