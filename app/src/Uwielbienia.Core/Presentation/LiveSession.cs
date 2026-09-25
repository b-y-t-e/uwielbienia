namespace Uwielbienia.Core.Presentation;

/// <summary>
/// Sterowanie ekranem. Każde źródło poleceń (klawiatura, przyciski, pilot, telefon) używa tylko tego interfejsu.
/// </summary>
public interface ILiveControl
{
    void Show(LiveItem item, int slideIndex = 0);

    void Next();

    void Previous();

    void GoToSlide(int slideIndex);

    void ToggleBlank();

    /// <summary>Zdejmuje wszystko z ekranu (pusty ekran bez pieśni).</summary>
    void Clear();
}

/// <summary>Migawka tego, co widzi sala. Niezmienna — bezpiecznie przekazywana do okien i przez sieć.</summary>
/// <param name="Next">Co pokaże „Następny”: etykieta slajdu albo tytuł kolejnej pieśni.</param>
public sealed record LiveState(
    long Version,
    LiveItem? Item,
    int SlideIndex,
    bool IsBlank,
    string? Next)
{
    public static readonly LiveState Empty = new(0, null, 0, false, null);

    public Slide? Slide => Item is { } item && SlideIndex < item.Slides.Count ? item.Slides[SlideIndex] : null;

    /// <summary>Slajd faktycznie widoczny na ekranie (<c>null</c> przy czarnym/pustym ekranie).</summary>
    public Slide? VisibleSlide => IsBlank ? null : Slide;
}

public interface ILiveStateSource
{
    LiveState State { get; }

    event EventHandler<LiveState>? StateChanged;
}

/// <summary>Jedno źródło prawdy o ekranie. Plan (playlista) służy tylko do przechodzenia między pieśniami.</summary>
public sealed class LiveSession : ILiveControl, ILiveStateSource
{
    private IReadOnlyList<LiveItem> _playlist = [];

    public LiveState State { get; private set; } = LiveState.Empty;

    public event EventHandler<LiveState>? StateChanged;

    /// <summary>
    /// Aktualizuje kolejkę pieśni (plan). Jeśli bieżąca pieśń jest w planie, jej slajdy są odświeżane —
    /// zmiana wybranych części działa od razu.
    /// </summary>
    public void SetPlaylist(IReadOnlyList<LiveItem> playlist)
    {
        _playlist = playlist;
        var current = State.Item;
        var refreshed = current?.PlanItemId is { } id ? playlist.FirstOrDefault(i => i.PlanItemId == id) : null;
        if (refreshed is not null)
            Publish(refreshed, Math.Min(State.SlideIndex, Math.Max(0, refreshed.Slides.Count - 1)), State.IsBlank);
        else
            Publish(current, State.SlideIndex, State.IsBlank);
    }

    public void Show(LiveItem item, int slideIndex = 0) =>
        Publish(item, Math.Clamp(slideIndex, 0, Math.Max(0, item.Slides.Count - 1)), isBlank: false);

    public void Next()
    {
        var item = State.Item;
        if (item is null)
        {
            if (_playlist.Count > 0)
                Show(_playlist[0]);
            return;
        }
        if (State.SlideIndex < item.Slides.Count - 1)
            Publish(item, State.SlideIndex + 1, State.IsBlank);
        else if (Neighbour(item, +1) is { } next)
            Publish(next, 0, State.IsBlank);
    }

    public void Previous()
    {
        var item = State.Item;
        if (item is null)
            return;
        if (State.SlideIndex > 0)
            Publish(item, State.SlideIndex - 1, State.IsBlank);
        else if (Neighbour(item, -1) is { } previous)
            Publish(previous, Math.Max(0, previous.Slides.Count - 1), State.IsBlank);
    }

    public void GoToSlide(int slideIndex)
    {
        if (State.Item is { } item && slideIndex >= 0 && slideIndex < item.Slides.Count)
            Publish(item, slideIndex, State.IsBlank);
    }

    public void ToggleBlank() => Publish(State.Item, State.SlideIndex, !State.IsBlank);

    public void Clear() => Publish(null, 0, isBlank: false);

    private LiveItem? Neighbour(LiveItem item, int step)
    {
        if (item.PlanItemId is not { } id)
            return null;
        var index = _playlist.ToList().FindIndex(i => i.PlanItemId == id);
        var target = index + step;
        return index >= 0 && target >= 0 && target < _playlist.Count ? _playlist[target] : null;
    }

    private void Publish(LiveItem? item, int slideIndex, bool isBlank)
    {
        State = new LiveState(State.Version + 1, item, slideIndex, isBlank, DescribeNext(item, slideIndex));
        StateChanged?.Invoke(this, State);
    }

    private string? DescribeNext(LiveItem? item, int slideIndex)
    {
        if (item is null)
            return _playlist.Count > 0 ? _playlist[0].Title : null;
        if (slideIndex < item.Slides.Count - 1)
            return item.Slides[slideIndex + 1].Label;
        return Neighbour(item, +1)?.Title;
    }
}
