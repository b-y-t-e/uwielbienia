using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Rysuje przejście: dwa zrzuty (stary i nowy slajd) połączone shaderem przy danym postępie. Stoi na miejscu obu slajdów
/// na czas przejścia (<see cref="ShaderPageTransition"/>); da się go też narysować samodzielnie (zrzuty do przeglądu).
/// </summary>
public sealed class ShaderTransitionView : Control
{
    private readonly SKImage _from;
    private readonly SKImage _to;
    private readonly SKRuntimeEffect _effect;
    private readonly float _seed;
    private readonly Vector _direction;
    private double _progress;

    /// <param name="direction">Kierunek ruchu (przesunięcie, promień światła), wektor jednostkowy.</param>
    public ShaderTransitionView(SKImage from, SKImage to, SKRuntimeEffect effect, float seed, Vector direction)
    {
        _from = from;
        _to = to;
        _effect = effect;
        _seed = seed;
        _direction = direction;
        IsHitTestVisible = false;
    }

    /// <summary>Rysuj shader także bez karty graficznej (wolno — tylko pojedyncze klatki, np. zrzuty do przeglądu).</summary>
    public bool AllowSoftware { get; init; }

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
        context.Custom(new Operation(new Rect(Bounds.Size), _from, _to, _effect, (float)_progress, _seed, _direction, AllowSoftware));

    private sealed class Operation(
        Rect bounds, SKImage from, SKImage to, SKRuntimeEffect effect, float progress, float seed, Vector direction, bool allowSoftware)
        : ICustomDrawOperation
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
            var width = (float)bounds.Width;
            var height = (float)bounds.Height;
            if (width <= 0 || height <= 0)
                return;
            if (lease.GrContext is null && !allowSoftware)
            {
                // Bez karty graficznej shader liczyłby procesor — klatki po kilkaset ms. Od razu nowa strona, a dalsze
                // przejścia to zwykłe przenikanie.
                if (TransitionShaders.DisableEffects())
                    TransitionLog.Write("EFEKTY WYŁĄCZONE do końca działania — brak karty graficznej (rysowanie programowe)");
                lease.SkCanvas.DrawImage(to, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Linear), null);
                return;
            }
            Draw(lease.SkCanvas, width, height);
        }

        private void Draw(SKCanvas canvas, float width, float height)
        {
            var sampling = new SKSamplingOptions(SKFilterMode.Linear);
            using var fromShader = from.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling,
                SKMatrix.CreateScale(width / from.Width, height / from.Height));
            using var toShader = to.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling,
                SKMatrix.CreateScale(width / to.Width, height / to.Height));
            // bez Dispose: zwolnienie buildera zwalnia też skompilowany efekt, wspólny dla wszystkich klatek
            var builder = new SKRuntimeShaderBuilder(effect);
            builder.Children["from"] = fromShader;
            builder.Children["to"] = toShader;
            builder.Uniforms["progress"] = progress;
            builder.Uniforms["resolution"] = new[] { width, height };
            builder.Uniforms["seed"] = seed;
            builder.Uniforms["direction"] = new[] { (float)direction.X, (float)direction.Y };
            using var shader = builder.Build();
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(SKRect.Create(width, height), paint);
        }
    }
}
