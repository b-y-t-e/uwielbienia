using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Zrzut ostatnio widocznej strony nad panelem przejścia, dopóki następna strona się nie wczyta (<see cref="CutTransition"/>).
/// <c>TransitioningContentControl</c> ma tylko dwa miejsca na strony: gdy obraz nowej strony jeszcze się wczytuje, a operator
/// przejdzie dalej, kolejna strona zajmuje miejsce widocznej — bez zrzutu na ekranie zostałoby samo tło.
/// Zrzut znika sam, gdy w panelu pojawi się coś nowego: wczytana strona staje się widoczna albo przejście dodaje swój widok —
/// inne przejścia (<see cref="Avalonia.Animation.IPageTransition"/>) nic o nim nie wiedzą.
/// </summary>
internal static class HeldFrame
{
    private static readonly ConditionalWeakTable<Panel, Control> Held = new();

    /// <summary>Zatrzymuje na ekranie obraz strony <paramref name="page"/> (aż do <see cref="Release"/>).</summary>
    public static void Hold(Visual page)
    {
        if (HostOf(page) is not { } host || ShaderPageTransition.Capture(page) is not { } image)
            return;
        Release(page);
        var view = new CrossFadeView(image, image) // postęp 0 = sam zrzut
        {
            Width = page.Bounds.Width,
            Height = page.Bounds.Height,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            ZIndex = 3,
        };
        host.Children.Add(view);
        Held.AddOrUpdate(host, view);
        WatchForNextPage(host, view);
        view.DetachedFromVisualTree += (_, _) =>
            _ = Task.Delay(500).ContinueWith(_ => image.Dispose(), TaskScheduler.Default); // ostatnia klatka mogła go jeszcze rysować
    }

    public static void Release(Visual? page)
    {
        if (HostOf(page) is not { } host || !Held.TryGetValue(host, out var view))
            return;
        ReleaseHeld(host, view);
    }

    /// <summary>Zdejmuje zrzut, gdy następne przejście coś pokaże: doda widok do panelu albo odsłoni wczytaną stronę.</summary>
    private static void WatchForNextPage(Panel host, Control view)
    {
        void OnChildAdded(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems?.Cast<object>().Any(c => c != view) == true)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ReleaseHeld(host, view)); // nie w trakcie zmiany listy dzieci
        }
        void OnPageChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if ((e.Property == Visual.OpacityProperty || e.Property == Visual.IsVisibleProperty)
                && sender is Visual { IsVisible: true, Opacity: > 0 } page && PageReadiness.IsLoaded(page))
                ReleaseHeld(host, view);
        }
        var pages = host.Children.Where(c => c != view).ToList();
        host.Children.CollectionChanged += OnChildAdded;
        foreach (var page in pages)
            page.PropertyChanged += OnPageChanged;
        view.DetachedFromVisualTree += (_, _) =>
        {
            host.Children.CollectionChanged -= OnChildAdded;
            foreach (var page in pages)
                page.PropertyChanged -= OnPageChanged;
        };
    }

    private static void ReleaseHeld(Panel host, Control view)
    {
        if (Held.TryGetValue(host, out var held) && held == view)
            Held.Remove(host);
        host.Children.Remove(view);
    }

    private static Panel? HostOf(Visual? page) => page?.GetVisualParent() as Panel;
}
