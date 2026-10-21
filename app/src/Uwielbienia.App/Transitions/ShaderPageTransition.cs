using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
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

    public IPageTransition Fallback { get; init; } = new DirectCrossFade(TimeSpan.FromSeconds(0.6));

    /// <summary>Przejście dla wybranych efektów: żadnego — zwykłe przenikanie.</summary>
    public static IPageTransition For(IReadOnlyList<SlideTransition> effects) =>
        effects.Count == 0 ? new DirectCrossFade(TimeSpan.FromSeconds(0.6)) : new ShaderPageTransition(effects);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var id = TransitionLog.NextId();
        // Strony należą teraz do tego przejścia — przerwane poprzednie nie może ich już odsłonić (mignięcie nowej strony
        // przed efektem: jej strona „przed” to strona „po” tego przejścia).
        TransitionPageOwners.Claim(from, id);
        TransitionPageOwners.Claim(to, id);
        var where = Where(to ?? from);
        if (cancellationToken.IsCancellationRequested)
        {
            TransitionLog.Write($"#{id} {where}: przerwane przed startem");
            return;
        }
        // Przed albo po nie ma treści (kiosk wchodzi na ekran albo z niego schodzi — to już ma własne przejście strony):
        // od razu, bez drugiego efektu naraz.
        if (from is ContentPresenter { Content: null } || to is ContentPresenter { Content: null })
        {
            TransitionLog.Write($"#{id} {where}: bez efektu — {(from is ContentPresenter { Content: null } ? "przed" : "po")} pusta strona");
            if (from is not null)
                from.IsVisible = false;
            if (to is not null)
            {
                to.IsVisible = true;
                to.Opacity = 1;
            }
            return;
        }
        var effect = SlideTransitions.Pick(Effects, Random);
        var shader = effect is { } chosen && TransitionShaders.IsSmooth ? TransitionShaders.For(chosen) : null;
        var picked = $"wybrane [{string.Join(", ", Effects.Select(SlideTransitions.Name))}] → {(effect is { } e ? SlideTransitions.Name(e) : "—")}";
        if (from is null || to is null || shader is null || to.GetVisualParent() is not Panel host)
        {
            var reason = from is null || to is null ? "brak starej lub nowej strony"
                : !TransitionShaders.IsSmooth ? "efekty wyłączone (wcześniej za wolne klatki albo brak karty graficznej)"
                : shader is null ? "shader się nie skompilował"
                : "strona nie leży w panelu";
            TransitionLog.Write($"#{id} {where}: {picked}; PRZENIKANIE — {reason}");
            await Fallback.Start(from, to, forward, cancellationToken);
            return;
        }

        // Nowa strona jeszcze niewidoczna — ekran pokazuje starą, dopóki nowa się nie ułoży i nie da się jej sfotografować.
        to.IsVisible = true;
        to.Opacity = 0;
        from.Opacity = 1; // po przerwanym przejściu mogła zostać ukryta
        if (await PageReadiness.WaitAsync(to, cancellationToken) is { } waited)
            TransitionLog.Write($"#{id} {where}: {waited}");
        if (cancellationToken.IsCancellationRequested)
        {
            TransitionLog.Write($"#{id} {where}: {picked}; przerwane (kolejna zmiana) przed zrzutem");
            if (TransitionPageOwners.Owns(to, id))
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
            TransitionLog.Write($"#{id} {where}: {picked}; PRZENIKANIE — nie udał się zrzut strony ({from.Bounds.Size} → {to.Bounds.Size})");
            await Fallback.Start(from, to, forward, cancellationToken);
            return;
        }
        TransitionLog.Write($"#{id} {where}: {picked}; start, zrzut {fromImage.Width}×{fromImage.Height}");

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
            await Animate(view, effect == SlideTransition.Slide ? SlideDuration : Duration, id, where, cancellationToken);
        }
        finally
        {
            host.Children.Remove(view);
            if (TransitionPageOwners.Owns(to, id))
                to.Opacity = 1;
            if (TransitionPageOwners.Owns(from, id))
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

    /// <summary>Gdzie rysuje się przejście: okno projekcji czy okno operatora (kolumna „Na ekranie”).</summary>
    private static string Where(Visual? visual) => TopLevel.GetTopLevel(visual) switch
    {
        Views.ProjectionWindow => "rzutnik",
        Views.MainWindow => "Na ekranie",
        { } other => other.GetType().Name,
        null => "poza oknem",
    };

    private static Task Animate(ShaderTransitionView view, TimeSpan duration, int id, string where, CancellationToken cancellationToken)
    {
        if (TopLevel.GetTopLevel(view) is not { } top)
        {
            TransitionLog.Write($"#{id} {where}: koniec od razu — widok efektu poza oknem");
            return Task.CompletedTask;
        }
        var done = new TaskCompletionSource();
        var smoothness = TransitionShaders.BeginTransition();
        TimeSpan? start = null;
        TimeSpan? previous = null;
        var frames = 0;
        var slow = 0;
        var longest = TimeSpan.Zero;
        void Finish(string how)
        {
            TransitionLog.Write($"#{id} {where}: {how}; klatek {frames}, najdłuższy odstęp {longest.TotalMilliseconds:0} ms, " +
                                $"wolnych (>{TransitionShaders.SlowFrame.TotalMilliseconds:0} ms) {slow}");
            done.TrySetResult();
        }
        void Frame(TimeSpan now)
        {
            // Odstępy między klatkami mierzą całe rysowanie (także na karcie graficznej); pierwszy pomijamy — kompilacja
            // shadera. Zbyt wolne pod rząd (albo brak karty graficznej) — przejście kończy się od razu, bez zacinania.
            frames++;
            if (previous is { } last && last != start)
            {
                var interval = now - last;
                if (interval > longest)
                    longest = interval;
                if (interval > TransitionShaders.SlowFrame)
                    slow++;
                if (smoothness.ReportFrame(interval))
                    TransitionLog.Write($"#{id} {where}: EFEKTY WYŁĄCZONE do końca działania — {TransitionShaders.SlowFramesInRow} wolnych klatek pod rząd (ostatnia {interval.TotalMilliseconds:0} ms)");
            }
            previous = now;
            if (cancellationToken.IsCancellationRequested)
            {
                Finish("przerwane kolejną zmianą");
                return;
            }
            if (!TransitionShaders.IsSmooth)
            {
                Finish("urwane — efekty wyłączone");
                return;
            }
            start ??= now;
            var progress = (now - start.Value) / duration;
            view.Progress = progress;
            if (progress >= 1)
                Finish("koniec");
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
        var image = ToSkImage(bitmap);
        return image is not null && BackgroundOf(visual) is { } background ? OnBackground(image, background) : image;
    }

    /// <summary>
    /// Tło, na którym leży strona (np. tło ekranu: czarne albo jasne). Strona z tekstem sama jest przezroczysta, a efekty
    /// rysują nieprzezroczyście — bez tła przezroczyste piksele zrzutu byłyby czarne.
    /// </summary>
    internal static Color? BackgroundOf(Visual visual) =>
        visual.GetSelfAndVisualAncestors()
            .Select(v => v switch
            {
                Panel panel => panel.Background,
                Border border => border.Background,
                TemplatedControl control => control.Background,
                _ => null,
            })
            .OfType<ISolidColorBrush>()
            .Where(brush => brush.Color.A > 0 && brush.Opacity > 0)
            .Select(brush => (Color?)brush.Color)
            .FirstOrDefault();

    private static SKImage OnBackground(SKImage image, Color background)
    {
        using (image)
        {
            using var surface = SKSurface.Create(new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(new SKColor(background.R, background.G, background.B));
            surface.Canvas.DrawImage(image, 0, 0);
            return surface.Snapshot();
        }
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
