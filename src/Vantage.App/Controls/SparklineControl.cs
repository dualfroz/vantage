using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vantage.App.Controls;

public sealed class SparklineControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineControl, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SparklineControl, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush>(nameof(Stroke), Brushes.DeepSkyBlue);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(Fill));

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(ValuesProperty, MaximumProperty, StrokeProperty, FillProperty);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (values is null || values.Count < 2 || width <= 0 || height <= 0)
            return;

        var max = Maximum <= 0 ? 1 : Maximum;
        var stepX = width / (values.Count - 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = new Point(0, ToY(values[0], max, height));
            ctx.BeginFigure(first, isFilled: Fill is not null);
            for (var i = 1; i < values.Count; i++)
                ctx.LineTo(new Point(i * stepX, ToY(values[i], max, height)));

            if (Fill is not null)
            {
                ctx.LineTo(new Point((values.Count - 1) * stepX, height));
                ctx.LineTo(new Point(0, height));
            }

            ctx.EndFigure(Fill is not null);
        }

        if (Fill is not null)
            context.DrawGeometry(Fill, null, geometry);

        context.DrawGeometry(null, new Pen(Stroke, 1.5), geometry);
    }

    private static double ToY(double value, double max, double height)
    {
        var clamped = Math.Clamp(value, 0, max);
        return height - clamped / max * height;
    }
}
