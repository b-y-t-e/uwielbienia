using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Przejście strony (<see cref="IPageTransition"/> — działa w każdym <c>TransitioningContentControl</c>, więc może kiedyś
/// służyć każdej zmianie slajdu): przy każdej zmianie losuje jeden z wybranych efektów, robi zrzut starej i nowej strony
/// i na czas przejścia rysuje je shaderem (<see cref="ShaderTransitionView"/>) w miejscu obu stron.
/// Bez wybranych efektów, bez zrzutu, gdy shader się nie skompilował albo komputer rysuje go za wolno — zwykłe przenikanie.
/// Wstecz (<c>forward = false</c>) przesunięcie jedzie w przeciwną stronę.
/// </summary>
public sealed class ShaderPageTransition(IReadOnlyList<SlideTransition> effects) : IPageTransition
{
    private static readonly Random Random = new();

    /// <summary>Największa szerokość zrzutu w pikselach (płótno rzutnika ma 1920).</summary>
    private const int MaxCaptureWidth = 2560;

    public IReadOnlyList<SlideTransition> Effects { get; } = effects;

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(1.2);

    /// <summary>Przesunięcie jest prostsze — krótsze.</summary>
    public TimeSpan SlideDuration { get; init; } = TimeSpan.FromSeconds(0.8);

    public IPageTransition Fallback { get; init; } = new CrossFade(TimeSpan.FromSeconds(0.6));

    /// <summary>Przejście dla wybranych efektów: żadnego — zwykłe przenikanie.</summary>
    public static IPageTransition For(IReadOnlyList<SlideTransition> effects) =>
        effects.Count == 0 ? new CrossFade(TimeSpan.FromSeconds(0.6)) : new ShaderPageTransition(effects);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;
        var effect = SlideTransitions.Pick(Effects, Random);
        var shader = effect is { } chosen && TransitionShaders.IsSmooth ? TransitionShaders.For(chosen) : null;
        if (from is null || to is null || shader is null || to.GetVisualParent() is not Panel host)
        {
            await Fallback.Start(from, to, forward, cancellationToken);
            return;
        }

        // Nowa strona jeszcze niewidoczna — ekran pokazuje starą, dopóki nowa się nie ułoży i nie da się jej sfotografować.
        to.IsVisible = true;
        to.Opacity = 0;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        if (cancellationToken.IsCancellationRequested)
        {
            to.Opacity = 1;
            return;
        }

        from.Opacity = 1;
        to.Opacity = 1;
        var fromImage = Capture(from);
        var toImage = Capture(to);
        if (fromImage is null || toImage is null)
        {
            fromImage?.Dispose();
            toImage?.Dispose();
            to.Opacity = 0;
            await Fallback.Start(from, to, forward, cancellationToken);
            return;
        }

        var view = new ShaderTransitionView(fromImage, toImage, shader, (float)Random.NextDouble(), Direction(effect!.Value, forward))
        {
            Width = to.Bounds.Width,
            Height = to.Bounds.Height,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        host.Children.Add(view);
        from.Opacity = 0;
        to.Opacity = 0;
        try
        {
            await Animate(view, effect == SlideTransition.Slide ? SlideDuration : Duration, cancellationToken);
        }
        finally
        {
            host.Children.Remove(view);
            to.Opacity = 1;
            from.Opacity = 1;
            if (!cancellationToken.IsCancellationRequested)
                from.IsVisible = false;
            // ostatnia klatka mogła jeszcze używać zrzutów — zwolnij je chwilę później
            _ = Task.Delay(500).ContinueWith(_ =>
            {
                fromImage.Dispose();
                toImage.Dispose();
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Przesunięcie: losowo w lewo, w prawo, w górę albo w dół; promień światła: losowa przekątna.</summary>
    private static Vector Direction(SlideTransition effect, bool forward)
    {
        var sign = forward ? 1 : -1;
        return effect switch
        {
            SlideTransition.Slide => Random.Next(4) switch
            {
                0 => new Vector(sign, 0),
                1 => new Vector(-sign, 0),
                2 => new Vector(0, sign),
                _ => new Vector(0, -sign),
            },
            SlideTransition.LightRay => Vector.Normalize(new Vector(Random.Next(2) == 0 ? 1 : -1, Random.Next(2) == 0 ? 0.45 : -0.45)),
            _ => new Vector(sign, 0),
        };
    }

    private static Task Animate(ShaderTransitionView view, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (TopLevel.GetTopLevel(view) is not { } top)
            return Task.CompletedTask;
        var done = new TaskCompletionSource();
        TimeSpan? start = null;
        TimeSpan? previous = null;
        void Frame(TimeSpan now)
        {
            // Odstępy między klatkami mierzą całe rysowanie (także na karcie graficznej); pierwszy pomijamy — kompilacja
            // shadera. Zbyt wolne pod rząd (albo brak karty graficznej) — przejście kończy się od razu, bez zacinania.
            if (previous is { } last && last != start)
                TransitionShaders.ReportFrame(now - last);
            previous = now;
            if (cancellationToken.IsCancellationRequested || !TransitionShaders.IsSmooth)
            {
                done.TrySetResult();
                return;
            }
            start ??= now;
            var progress = (now - start.Value) / duration;
            view.Progress = progress;
            if (progress >= 1)
                done.TrySetResult();
            else
                top.RequestAnimationFrame(Frame);
        }
        top.RequestAnimationFrame(Frame);
        return done.Task;
    }

    /// <summary>
    /// Zrzut strony w rozdzielczości, w jakiej jest widoczna (rzutnik ~1920 px, podgląd w oknie operatora — mniejszy).
    /// </summary>
    public static SKImage? Capture(Visual visual)
    {
        var size = visual.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
            return null;
        var scale = 1.0;
        if (TopLevel.GetTopLevel(visual) is { } top && visual.TransformToVisual(top) is { } matrix)
            scale = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12) * top.RenderScaling;
        scale = Math.Clamp(scale, 0.1, MaxCaptureWidth / size.Width);
        var pixels = new PixelSize(Math.Max(1, (int)Math.Round(size.Width * scale)), Math.Max(1, (int)Math.Round(size.Height * scale)));
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
        bitmap.Render(visual);
        return ToSkImage(bitmap);
    }

    public static SKImage? ToSkImage(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var colorType = bitmap.Format == PixelFormat.Rgba8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        var info = new SKImageInfo(size.Width, size.Height, colorType, SKAlphaType.Premul);
        var buffer = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, info.RowBytes);
        }
        finally
        {
            handle.Free();
        }
        return SKImage.FromPixelCopy(info, buffer, info.RowBytes);
    }
}
