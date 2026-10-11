using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Claudius.Core;

namespace Claudius.Views;

/// <summary>
/// The course of one rate-limit window from its start to its reset: the usage so far as a step line
/// (samples are only written when the value changes) with a light wash below, a dot at "now" and the
/// forecast dashed from there. 0 to 100 % from bottom to top. Hovering shows the time and value under the pointer.
/// </summary>
public sealed class UsageChart : FrameworkElement
{
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(UsageChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Color of the line; lightened if it would not stand out from the overlay.</summary>
    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    private static readonly Color Surface = Color.FromRgb(0x26, 0x26, 0x24);
    private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x36))), 1));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0xD2, 0xC8)));
    private static readonly Brush SurfaceBrush = Freeze(new SolidColorBrush(Surface));
    private static readonly Typeface LabelFace = new("Segoe UI");
    private const double Inset = 5; // room for the end dot at 0 and 100 %

    private IReadOnlyList<(DateTimeOffset Time, double Percent)> _points = [];
    private DateTimeOffset _start, _end, _now;
    private (DateTimeOffset Time, double Percent)? _forecast;
    private Func<DateTimeOffset, string> _describeTime = Format.Clock;
    private double? _hoverX;

    public UsageChart()
    {
        MouseMove += (_, e) => { _hoverX = e.GetPosition(this).X; InvalidateVisual(); };
        MouseLeave += (_, _) => { _hoverX = null; InvalidateVisual(); };
    }

    /// <summary>
    /// <paramref name="points"/> in time order within the window from <paramref name="start"/> to
    /// <paramref name="end"/>; <paramref name="forecast"/> is where the dashed line ends.
    /// </summary>
    public void SetData(IReadOnlyList<(DateTimeOffset Time, double Percent)> points, DateTimeOffset start,
        DateTimeOffset end, DateTimeOffset now, (DateTimeOffset Time, double Percent)? forecast,
        Func<DateTimeOffset, string> describeTime)
    {
        _points = points;
        _start = start;
        _end = end;
        _now = now < end ? now : end;
        _forecast = forecast;
        _describeTime = describeTime;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        // Transparent background so the hover works over the whole chart, not only on the line.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        double top = Inset, bottom = h - Inset;
        dc.DrawLine(GridPen, new Point(0, top + 0.5), new Point(w, top + 0.5));
        dc.DrawLine(GridPen, new Point(0, bottom + 0.5), new Point(w, bottom + 0.5));
        if (_points.Count == 0 || _end <= _start) return;

        double X(DateTimeOffset t) => Math.Clamp((t - _start) / (_end - _start), 0, 1) * w;
        double Y(double p) => bottom - Math.Clamp(p, 0, 100) / 100 * (bottom - top);

        var color = Readable(Stroke is SolidColorBrush solid ? solid.Color : Colors.Gray);
        var brush = Freeze(new SolidColorBrush(color));
        var line = Freeze(new Pen(brush, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });

        // Usage so far: each value holds until the next sample, the last one until now.
        var steps = new List<Point>();
        foreach (var (time, percent) in _points)
        {
            if (steps.Count > 0) steps.Add(new Point(X(time), steps[^1].Y));
            steps.Add(new Point(X(time), Y(percent)));
        }
        var nowPoint = new Point(X(_now), steps[^1].Y);
        steps.Add(nowPoint);

        var wash = new StreamGeometry();
        using (var g = wash.Open())
        {
            g.BeginFigure(new Point(steps[0].X, bottom), true, true);
            g.PolyLineTo(steps, false, true);
            g.LineTo(new Point(nowPoint.X, bottom), false, true);
        }
        wash.Freeze();
        dc.DrawGeometry(Freeze(new SolidColorBrush(Color.FromArgb(0x1A, color.R, color.G, color.B))), null, wash);

        var path = new StreamGeometry();
        using (var g = path.Open())
        {
            g.BeginFigure(steps[0], false, false);
            g.PolyLineTo(steps, true, true);
        }
        path.Freeze();
        dc.DrawGeometry(null, line, path);

        if (_forecast is { } f)
        {
            var dashed = new Pen(brush, 2) { DashStyle = new DashStyle([2, 2], 0), DashCap = PenLineCap.Flat };
            dashed.Freeze();
            dc.DrawLine(dashed, nowPoint, new Point(X(f.Time), Y(f.Percent)));
        }

        // The end dot carries a ring in the surface color so it stays clear where the dashed line leaves it.
        dc.DrawEllipse(brush, new Pen(SurfaceBrush, 2), nowPoint, 4, 4);

        if (_hoverX is { } hx) DrawHover(dc, hx, w, top, bottom, brush, X, Y);
    }

    private void DrawHover(DrawingContext dc, double hx, double w, double top, double bottom, Brush brush,
        Func<DateTimeOffset, double> x, Func<double, double> y)
    {
        var time = _start + (_end - _start) * Math.Clamp(hx / w, 0, 1);
        double? value = null;
        bool projected = false;
        if (time >= _points[0].Time && time <= _now)
        {
            value = _points.Last(p => p.Time <= time).Percent;
        }
        else if (time > _now && _forecast is { } f && time <= f.Time)
        {
            // Along the dashed line.
            double from = _points[^1].Percent;
            value = from + (f.Percent - from) * ((time - _now) / (f.Time - _now));
            projected = true;
        }
        if (value is not { } v) return;

        double px = x(time), py = y(v);
        dc.DrawLine(GridPen, new Point(px + 0.5, top), new Point(px + 0.5, bottom));
        dc.DrawEllipse(brush, new Pen(SurfaceBrush, 2), new Point(px, py), 4, 4);

        string text = _describeTime(time) + " · " + (projected ? "≈ " : "") + Strings.Percent(Format.Percent(v));
        var label = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 11,
            TextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        // Beside the crosshair, on the side with room, and clear of the point.
        double lx = px + 8 + label.Width <= w ? px + 8 : px - 8 - label.Width;
        double ly = py - top > label.Height + 2 ? top : bottom - label.Height;
        dc.DrawRectangle(SurfaceBrush, null, new Rect(lx - 3, ly - 1, label.Width + 6, label.Height + 2));
        dc.DrawText(label, new Point(lx, ly));
    }

    /// <summary>The color, lightened towards white until it reaches 3:1 against the overlay.</summary>
    private static Color Readable(Color color)
    {
        for (double mix = 0; mix <= 1; mix += 0.1)
        {
            var c = Color.FromRgb(Blend(color.R, mix), Blend(color.G, mix), Blend(color.B, mix));
            if ((Luminance(c) + 0.05) / (Luminance(Surface) + 0.05) >= 3) return c;
        }
        return Colors.White;

        static byte Blend(byte v, double mix) => (byte)Math.Round(v + (255 - v) * mix);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
