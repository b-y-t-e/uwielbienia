using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Uwielbienia.App.Views;

/// <summary>
/// Powolny zoom zdjęcia (efekt Kena Burnsa): przez <see cref="DurationProperty"/> obraz przybliża się albo oddala o kilka
/// procent i lekko przesuwa, za każdym razem w losową stronę. Rusza dopiero po przejściu do tego elementu (<see cref="Delay"/>) —
/// przejście fotografuje go w pozycji początkowej, więc po przejściu nic nie skacze. Rodzic powinien przycinać (ClipToBounds).
/// </summary>
public static class SlowZoom
{
    /// <summary>Tyle trwa przejście kiosku — zoom czeka na jego koniec.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(1.2);

    private const double Zoom = 1.07;
    /// <summary>Największe przesunięcie w pikselach płótna 1920×1080.</summary>
    private const double Shift = 32;

    private static readonly Random Random = new();
    private static readonly ConditionalWeakTable<Visual, Animatable> Running = [];

    /// <summary>Jak długo trwa zoom; zero = bez zoomu.</summary>
    public static readonly AttachedProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterAttached<Visual, TimeSpan>("Duration", typeof(SlowZoom));

    static SlowZoom()
    {
        DurationProperty.Changed.AddClassHandler<Visual>((visual, _) => Restart(visual));
    }

    public static TimeSpan GetDuration(Visual visual) => visual.GetValue(DurationProperty);

    public static void SetDuration(Visual visual, TimeSpan value) => visual.SetValue(DurationProperty, value);

    private static void Restart(Visual visual)
    {
        Stop(visual);
        visual.AttachedToVisualTree -= OnAttached;
        visual.DetachedFromVisualTree -= OnDetached;
        if (GetDuration(visual) <= TimeSpan.Zero)
        {
            visual.RenderTransform = null;
            return;
        }
        visual.AttachedToVisualTree += OnAttached;
        visual.DetachedFromVisualTree += OnDetached;
        if (visual.GetVisualParent() is not null)
            Start(visual);
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Start((Visual)sender!);

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Stop((Visual)sender!);

    private static void Start(Visual visual)
    {
        Stop(visual);
        var duration = GetDuration(visual);
        if (duration <= TimeSpan.Zero || visual is not Animatable animatable)
            return;
        // przybliżenie albo oddalenie, przesunięcie w losową stronę
        var zoomIn = Random.Next(2) == 0;
        var dx = (Random.NextDouble() * 2 - 1) * Shift;
        var dy = (Random.NextDouble() * 2 - 1) * Shift;
        var near = Transform(Zoom, dx, dy);
        var far = Transform(1, 0, 0);
        // Avalonia nie animuje RenderTransform klatkami kluczowymi (Animation) — tylko przejściem: pozycja początkowa od
        // razu, końcowa z przejściem trwającym cały czas elementu.
        animatable.Transitions = null;
        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = zoomIn ? far : near;
        animatable.Transitions =
        [
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = duration,
                Delay = Delay,
                Easing = new SineEaseInOut(),
            },
        ];
        visual.RenderTransform = zoomIn ? near : far;
        Running.AddOrUpdate(visual, animatable);
    }

    private static TransformOperations Transform(double scale, double dx, double dy)
    {
        var builder = TransformOperations.CreateBuilder(2);
        builder.AppendTranslate(dx, dy);
        builder.AppendScale(scale, scale);
        return builder.Build();
    }

    private static void Stop(Visual visual)
    {
        if (Running.TryGetValue(visual, out var animatable))
        {
            animatable.Transitions = null;
            Running.Remove(visual);
        }
    }
}
