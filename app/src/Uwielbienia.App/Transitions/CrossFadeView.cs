using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Przenikanie dwóch zrzutów (<see cref="DirectCrossFade"/>): stary w całości, nowy na nim z kryciem równym postępowi —
/// jedno rysowanie na zrzut, więc wynik to dokładna mieszanka obu (także bez karty graficznej; to tanie).
/// </summary>
public sealed class CrossFadeView : Control
{
    private readonly SKImage _from;
    private readonly SKImage _to;
    private double _progress;

    public CrossFadeView(SKImage from, SKImage to)
    {
        _from = from;
        _to = to;
        IsHitTestVisible = false;
    }

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 1);
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context) =>
        context.Custom(new Operation(new Rect(Bounds.Size), _from, _to, (float)_progress));

    private sealed class Operation(Rect bounds, SKImage from, SKImage to, float progress) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                return;
            using var lease = feature.Lease();
            var rect = SKRect.Create((float)bounds.Width, (float)bounds.Height);
            var sampling = new SKSamplingOptions(SKFilterMode.Linear);
            lease.SkCanvas.DrawImage(from, rect, sampling, null);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(progress * 255)) };
            lease.SkCanvas.DrawImage(to, rect, sampling, paint);
        }
    }
}
