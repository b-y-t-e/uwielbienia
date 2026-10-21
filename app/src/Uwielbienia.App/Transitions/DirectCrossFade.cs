using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Przenikanie wprost ze starej strony w nową, bez przyciemnienia. Stara zostaje, dopóki nowa nie jest gotowa
/// (<see cref="PageReadiness"/>), potem w miejscu obu stron <see cref="CrossFadeView"/> miesza ich zrzuty (z tłem ekranu).
/// Nie przezroczystość samej strony: Avalonia nakłada ją na każdy element osobno — tło strony i obraz kolejno przyciemniały
/// starą stronę pod spodem i w połowie przejścia ekran był ciemniejszy niż obie strony (czarne mignięcie). Tak samo
/// <see cref="CrossFade"/> z Avalonii (wygasza starą i rozjaśnia nową naraz — prześwituje tło).
/// </summary>
public sealed class DirectCrossFade(TimeSpan duration) : IPageTransition
{
    public TimeSpan Duration { get; } = duration;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var id = TransitionLog.NextId();
        TransitionPageOwners.Claim(from, id);
        TransitionPageOwners.Claim(to, id);
        if (cancellationToken.IsCancellationRequested)
            return;
        if (to is null)
        {
            if (from is not null)
                from.IsVisible = false;
            return;
        }
        if (from is not null)
        {
            from.ZIndex = 0;
            from.Opacity = 1;
            from.IsVisible = true;
        }
        to.ZIndex = 1;
        to.Opacity = 0;
        to.IsVisible = true;
        try
        {
            if (await PageReadiness.WaitAsync(to, cancellationToken) is { } waited)
                TransitionLog.Write($"#{id} przenikanie: {waited}");
            if (cancellationToken.IsCancellationRequested)
                return;
            if (from is null || to.GetVisualParent() is not Panel host || !await FadeCapturesAsync(from, to, host, cancellationToken))
                await FadePageAsync(to, cancellationToken);
        }
        finally
        {
            // przerwane: strona może już należeć do następnego przejścia — wtedy jej nie odsłaniaj
            if (TransitionPageOwners.Owns(to, id))
                to.Opacity = 1;
            if (from is not null && TransitionPageOwners.Owns(from, id))
                from.Opacity = 1;
            if (!cancellationToken.IsCancellationRequested && from is not null)
                from.IsVisible = false;
        }
    }

    /// <summary>Przenikanie zrzutów obu stron; <c>false</c>, gdy zrzut się nie udał.</summary>
    private async Task<bool> FadeCapturesAsync(Visual from, Visual to, Panel host, CancellationToken cancellationToken)
    {
        to.Opacity = 1;
        var fromImage = ShaderPageTransition.Capture(from);
        var toImage = ShaderPageTransition.Capture(to);
        to.Opacity = 0;
        if (fromImage is null || toImage is null)
        {
            fromImage?.Dispose();
            toImage?.Dispose();
            return false;
        }
        var view = new CrossFadeView(fromImage, toImage)
        {
            Width = to.Bounds.Width,
            Height = to.Bounds.Height,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            ZIndex = 2,
        };
        host.Children.Add(view);
        from.Opacity = 0;
        try
        {
            await AnimateAsync(view, progress => view.Progress = progress, Duration, cancellationToken);
        }
        finally
        {
            host.Children.Remove(view);
            // ostatnia klatka mogła jeszcze używać zrzutów — zwolnij je chwilę później
            _ = Task.Delay(500).ContinueWith(_ =>
            {
                fromImage.Dispose();
                toImage.Dispose();
            }, TaskScheduler.Default);
        }
        return true;
    }

    /// <summary>Bez zrzutów (np. nie ma starej strony): nowa strona pojawia się sama, na tle ekranu.</summary>
    private async Task FadePageAsync(Visual to, CancellationToken cancellationToken)
    {
        ISolidColorBrush? background = null;
        if (to is ContentPresenter presenter && ShaderPageTransition.BackgroundOf(presenter) is { } color)
            presenter.Background = background = new ImmutableSolidColorBrush(color);
        try
        {
            await AnimateAsync(to, progress => to.Opacity = progress, Duration, cancellationToken);
        }
        finally
        {
            // tło tylko na czas przejścia — po zmianie tła ekranu zostałoby stare
            if (to is ContentPresenter p && background is not null && ReferenceEquals(p.Background, background))
                p.Background = null;
        }
    }

    /// <summary>
    /// Postęp 0 → 1 liczony w każdej klatce okna. Nie <see cref="Animation"/>: ta z <c>FillMode.Forward</c> trzyma po końcu
    /// wartość ponad wartością lokalną.
    /// </summary>
    private static Task AnimateAsync(Visual visual, Action<double> apply, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (TopLevel.GetTopLevel(visual) is not { } top || duration <= TimeSpan.Zero)
            return Task.CompletedTask;
        var done = new TaskCompletionSource();
        TimeSpan? start = null;
        void Frame(TimeSpan now)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                done.TrySetResult();
                return;
            }
            start ??= now;
            var progress = Math.Clamp((now - start.Value) / duration, 0, 1);
            apply(progress);
            if (progress >= 1)
                done.TrySetResult();
            else
                top.RequestAnimationFrame(Frame);
        }
        top.RequestAnimationFrame(Frame);
        return done.Task;
    }
}
