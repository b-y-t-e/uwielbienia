using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Views;

/// <summary>
/// Ramka i narożniki obrazu na ekranie projekcji (po ruchu myszą albo w trybie kalibracji).
/// Wypełniony punkt = narożnik, który przesunie przeciąganie (najbliższy kursora).
/// </summary>
public sealed class CalibrationHandles : Control
{
    private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(255, 160, 0));
    private static readonly IBrush AccentSoft = new SolidColorBrush(Color.FromArgb(90, 255, 160, 0));
    private static readonly IPen Outline = new Pen(Accent, 2);

    public static readonly StyledProperty<IReadOnlyList<CornerPoint>> CornersProperty =
        AvaloniaProperty.Register<CalibrationHandles, IReadOnlyList<CornerPoint>>(nameof(Corners), Keystone.FullScreen);

    public static readonly StyledProperty<int> SelectedCornerProperty =
        AvaloniaProperty.Register<CalibrationHandles, int>(nameof(SelectedCorner));

    static CalibrationHandles() => AffectsRender<CalibrationHandles>(CornersProperty, SelectedCornerProperty);

    public CalibrationHandles() => IsHitTestVisible = false;

    public IReadOnlyList<CornerPoint> Corners
    {
        get => GetValue(CornersProperty);
        set => SetValue(CornersProperty, value);
    }

    public int SelectedCorner
    {
        get => GetValue(SelectedCornerProperty);
        set => SetValue(SelectedCornerProperty, value);
    }

    public Point[] CornersInPixels() =>
        [.. Corners.Select(c => new Point(c.X * Bounds.Width, c.Y * Bounds.Height))];

    public override void Render(DrawingContext context)
    {
        var points = CornersInPixels();
        context.DrawGeometry(null, Outline, new PolylineGeometry([.. points, points[0]], isFilled: false));
        for (var i = 0; i < points.Length; i++)
            context.DrawEllipse(i == SelectedCorner ? Accent : AccentSoft, Outline, points[i], 14, 14);
    }
}

/// <summary>
/// Siatka kalibracyjna rysowana razem z obrazem (więc przekształcona tak samo): linie, przekątne, okrąg
/// i ramka — po nich łatwo wyrównać krawędzie i proporcje obrazu z rzutnika.
/// </summary>
public sealed class CalibrationGrid : Control
{
    private static readonly IPen Thin = new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), 2);
    private static readonly IPen Thick = new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 0)), 6);

    public CalibrationGrid() => IsHitTestVisible = false;

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;
        var step = h / 8;
        for (var x = w / 2 % step; x < w; x += step)
            context.DrawLine(Thin, new Point(x, 0), new Point(x, h));
        for (var y = step; y < h; y += step)
            context.DrawLine(Thin, new Point(0, y), new Point(w, y));
        context.DrawLine(Thin, new Point(0, 0), new Point(w, h));
        context.DrawLine(Thin, new Point(w, 0), new Point(0, h));
        context.DrawEllipse(null, Thick, new Point(w / 2, h / 2), h * 0.4, h * 0.4);
        context.DrawRectangle(null, Thick, new Rect(3, 3, w - 6, h - 6));
    }
}
