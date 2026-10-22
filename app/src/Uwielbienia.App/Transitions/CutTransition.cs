using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls.Presenters;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// „Brak efektu”: nowa strona od razu w miejsce starej. Stara zostaje, dopóki nowa nie jest gotowa (<see cref="PageReadiness"/>) —
/// inaczej przy wczytywanym obrazie mignęłoby puste tło. Gdy stara strona zniknie przed wczytaniem nowej (szybkie „Dalej”),
/// widać jej zrzut (<see cref="HeldFrame"/>).
/// </summary>
public sealed class CutTransition : IPageTransition
{
    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var id = TransitionLog.NextId();
        TransitionPageOwners.Claim(from, id);
        TransitionPageOwners.Claim(to, id);
        if (cancellationToken.IsCancellationRequested)
            return;
        if (to is not null)
        {
            if (from is not null && PageReadiness.IsLoaded(from)) // niewczytanej strony (pominiętej szybką zmianą) nie odsłaniamy
            {
                from.Opacity = 1;
                from.IsVisible = true;
                if (!PageReadiness.IsLoaded(to))
                    HeldFrame.Hold(from);
            }
            to.Opacity = 0;
            to.IsVisible = true;
            try
            {
                await LoadedAsync(to, cancellationToken);
                if (await PageReadiness.WaitAsync(to, cancellationToken) is { } waited)
                    TransitionLog.Write($"#{id} bez efektu: {waited}");
            }
            finally
            {
                if (TransitionPageOwners.Owns(to, id))
                    to.Opacity = 1;
            }
            if (cancellationToken.IsCancellationRequested)
                return; // zrzut zostaje — znika sam, gdy następne przejście coś pokaże (HeldFrame)
            HeldFrame.Release(to);
        }
        if (from is not null)
        {
            from.IsVisible = false;
            from.Opacity = 1;
        }
    }

    /// <summary>Czeka na wczytanie obrazu nowej strony bez limitu czasu — do tej pory widać starą (albo do anulowania zmiany).</summary>
    private static async Task LoadedAsync(Visual page, CancellationToken cancellationToken)
    {
        if (page is ContentPresenter { Content: IWaitablePage waitable })
            await waitable.Ready.WaitAsync(cancellationToken).ContinueWith(_ => { }, TaskScheduler.Current);
    }
}
