using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Vantage.Core.Models;

namespace Vantage.App.Converters;

public enum StatusTone
{
    Neutral,
    Ok,
    Warn,
    Down
}

public sealed class HealthStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        HealthStatus.Up => Brushes.LimeGreen,
        HealthStatus.Degraded => new SolidColorBrush(Color.Parse("#FFB300")),
        HealthStatus.Down => new SolidColorBrush(Color.Parse("#EF5350")),
        _ => new SolidColorBrush(Color.Parse("#77808B"))
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToBrushConverter : IValueConverter
{
    public IBrush TrueBrush { get; init; } = Brushes.LimeGreen;
    public IBrush FalseBrush { get; init; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ToneToBrushConverter : IValueConverter
{
    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#77808B"));
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#FFB300"));
    private static readonly IBrush Down = new SolidColorBrush(Color.Parse("#EF5350"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StatusTone.Ok => Ok,
        StatusTone.Warn => Warn,
        StatusTone.Down => Down,
        _ => Neutral
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class StateBrushConverters
{
    public static readonly BoolToBrushConverter RunningBrush = new()
    {
        TrueBrush = new SolidColorBrush(Color.Parse("#4CAF50")),
        FalseBrush = new SolidColorBrush(Color.Parse("#77808B"))
    };

    public static readonly HealthStatusToBrushConverter HealthBrush = new();

    public static readonly ToneToBrushConverter ToneBrush = new();
}
