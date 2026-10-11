using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MoveMouse.Converters;

/// <summary>
/// Maps the jiggler's running state to a status-dot color. Uses fixed,
/// explicit colors (rather than a named theme resource lookup) because in
/// this project's unpackaged deployment, named theme resources are not
/// reliably resolved — see the styling note at the top of MainWindow.xaml.
/// </summary>
public sealed class BoolToStatusBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush RunningBrush = new(Color.FromArgb(255, 16, 137, 62));
    private static readonly SolidColorBrush StoppedBrush = new(Color.FromArgb(255, 138, 138, 138));

    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is bool isRunning && isRunning) ? RunningBrush : StoppedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
