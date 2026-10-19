using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Kiosks;

/// <summary>
/// Zmiana elementów kiosku na ekranie — jedna dla rzutnika, kolumny „Na ekranie” i telefonu. Gdy kiosk jest na ekranie,
/// co <see cref="KioskSlide.Interval"/> pokazuje następny element (w kółko). Czarny ekran zatrzymuje zmianę; kiosk, do
/// którego się wraca, zaczyna od elementu, na którym skończył (osobno dla każdej pozycji planu).
/// </summary>
public sealed class KioskRotation : IDisposable
{
    private readonly ILiveStateSource _live;
    private readonly IUiDispatcher? _dispatcher;
    private readonly Timer? _timer;
    private readonly Dictionary<Guid, int> _resume = [];
    private Guid? _item;
    private int _generation;

    /// <param name="dispatcher">Bez niego (testy) nic nie zmienia się samo — tylko przez <see cref="Advance"/>.</param>
    public KioskRotation(ILiveStateSource live, IUiDispatcher? dispatcher = null)
    {
        _live = live;
        _dispatcher = dispatcher;
        if (dispatcher is not null)
            _timer = new Timer(OnTimer);
        live.StateChanged += OnStateChanged;
        Apply(live.State);
    }

    /// <summary>Kiosk na ekranie (także pod czarnym ekranem); <c>null</c> = ekran pokazuje coś innego.</summary>
    public KioskSlide? Slide { get; private set; }

    public int Index { get; private set; }

    /// <summary>Pozycja planu z kioskiem na ekranie (<see cref="Guid.Empty"/> = spoza planu); <c>null</c> = bez kiosku.</summary>
    public Guid? ItemId => _item;

    public int Count => Slide?.Frames.Count ?? 0;

    public KioskFrame? Frame => Slide is { } slide && Index < slide.Frames.Count ? slide.Frames[Index] : null;

    /// <summary>Elementy zmieniają się same (kiosk widoczny, co najmniej dwa elementy).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Inny element, inny kiosk albo zatrzymanie / wznowienie zmiany (na wątku interfejsu).</summary>
    public event EventHandler? Changed;

    /// <summary>Następny element (po ostatnim — pierwszy); czas do kolejnej zmiany liczy się od nowa.</summary>
    public void Advance() => GoTo(Index + 1);

    public void GoTo(int index)
    {
        if (Count == 0)
            return;
        Index = (index % Count + Count) % Count;
        Restart();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _live.StateChanged -= OnStateChanged;
        _timer?.Dispose();
    }

    private void OnStateChanged(object? sender, LiveState state) => Apply(state);

    private void Apply(LiveState state)
    {
        var slide = state.Slide as KioskSlide;
        Guid? item = slide is null ? null : state.Item?.PlanItemId ?? Guid.Empty;
        var changed = false;
        var restart = false;
        if (item != _item)
        {
            if (_item is { } previous && Slide is not null)
                _resume[previous] = Index;
            _item = item;
            Slide = slide;
            Index = item is { } id && slide is not null && _resume.TryGetValue(id, out var resumed) ? resumed % slide.Frames.Count : 0;
            changed = restart = true;
        }
        else if (slide is not null && !Equals(slide, Slide))
        {
            // Zmieniona treść tego samego kiosku (edycja, gotowe slajdy prezentacji): ten sam element, jeśli jest.
            var frame = Frame;
            restart = slide.Interval != Slide?.Interval;
            Slide = slide;
            var same = frame is null ? -1 : slide.Frames.ToList().IndexOf(frame);
            Index = same >= 0 ? same : Math.Min(Index, slide.Frames.Count - 1);
            changed = true;
        }

        var running = slide is { Frames.Count: > 1 } && !state.IsBlank;
        if (running != IsRunning)
        {
            IsRunning = running;
            changed = restart = true;
        }
        if (restart)
            Restart();
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pełny czas do następnej zmiany; tyknięcia zaplanowane wcześniej przestają się liczyć.</summary>
    private void Restart()
    {
        var generation = Interlocked.Increment(ref _generation);
        if (_timer is null)
            return;
        // numer przed uruchomieniem timera — tyknięcie, które odpali od razu, musi go już widzieć (inaczej zostałoby
        // odrzucone i nic nie zaplanowałoby następnego: kiosk stanąłby na stałe)
        Volatile.Write(ref _scheduled, generation);
        if (IsRunning && Slide is { } slide)
            _timer.Change(slide.Interval, Timeout.InfiniteTimeSpan);
        else
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private int _scheduled;

    private void OnTimer(object? state)
    {
        var generation = Volatile.Read(ref _scheduled);
        // zmiana na wątku interfejsu; tyknięcie sprzed ręcznej zmiany albo innego kiosku nic nie robi
        _ = _dispatcher!.InvokeAsync(() =>
        {
            if (generation == _generation && IsRunning)
                Advance();
            return true;
        });
    }
}
