using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace MoveMouse.Converters;

/// <summary>
/// Maps the jiggler's running state to one of WinUI 3's built-in semantic
/// fill brushes, so the status indicator automatically adapts to light and
/// dark theme without any custom colors.
/// </summary>
public sealed class BoolToStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isRunning = value is bool b && b;
        var resourceKey = isRunning ? "SystemFillColorSuccessBrush" : "SystemFillColorNeutralBrush";

        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(isRunning ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
