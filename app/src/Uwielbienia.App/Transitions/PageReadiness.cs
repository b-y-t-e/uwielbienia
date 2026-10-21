using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Czekanie, aż nowa strona da się pokazać — przed przejściem na nią. Strona pokazywana pierwszy raz (szablony, obraz
/// wczytywany w tle) przez pierwsze klatki jest samym tłem: przejście prowadziłoby najpierw do pustego ekranu.
/// </summary>
internal static class PageReadiness
{
    /// <summary>Najdłużej czeka na wczytanie nowej strony (<see cref="IWaitablePage"/>) — potem przejście i tak rusza.</summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Czeka na wczytanie i ułożenie strony; zwraca opis czekania do dziennika (albo <c>null</c>, gdy była gotowa).</summary>
    public static async Task<string?> WaitAsync(Visual page, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        if (page is ContentPresenter { Content: IWaitablePage waitable } && !waitable.Ready.IsCompleted)
        {
            // obraz jeszcze się wczytuje — bez tego nowa strona byłaby pustym tłem
            var waited = await Task.WhenAny(waitable.Ready, Task.Delay(ReadyTimeout, cancellationToken)) == waitable.Ready;
            notes.Add(waited ? "czekał na wczytanie nowej strony" : $"nowa strona nie wczytała się w {ReadyTimeout.TotalSeconds:0} s");
        }
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        // Strona pokazywana pierwszy raz układa się dopiero w kolejnej klatce.
        var pending = Pending(page);
        if (pending > 0)
        {
            await NextFramesAsync(page, 2);
            notes.Add($"nowa strona nieułożona ({pending} el.) — po 2 klatkach {Pending(page)} el.");
        }
        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>Elementy strony jeszcze nieułożone albo obrazy bez wczytanego źródła.</summary>
    private static int Pending(Visual page) =>
        page.GetSelfAndVisualDescendants().Count(v =>
            v is Layoutable { IsVisible: true } l && (!l.IsMeasureValid || !l.IsArrangeValid)
            || v is Image { IsVisible: true, Source: null });

    /// <summary>Czeka na <paramref name="count"/> klatek okna (w każdej najpierw układ), najwyżej 250 ms.</summary>
    private static async Task NextFramesAsync(Visual visual, int count)
    {
        if (TopLevel.GetTopLevel(visual) is not { } top)
            return;
        for (var i = 0; i < count; i++)
        {
            var frame = new TaskCompletionSource();
            top.RequestAnimationFrame(_ => frame.TrySetResult());
            await Task.WhenAny(frame.Task, Task.Delay(250));
        }
    }
}
