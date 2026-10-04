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
    /// Aktualizuje kolejkę pieśni (plan). Gdy w planie zmienił się układ części pieśni, która jest
    /// na ekranie, zmiana działa na żywo: ten sam slajd zostaje na ekranie, a jeśli jego część
    /// pominięto — ekran przechodzi do najbliższej dalszej (albo wcześniejszej) zachowanej części.
    /// Gdy pominięto wszystkie części, ekran nie pokazuje tekstu, a „Dalej” przechodzi do następnej pieśni.
    /// </summary>
    public void SetPlaylist(IReadOnlyList<LiveItem> playlist)
    {
        _playlist = playlist;
        var current = State.Item;
        var slideIndex = State.SlideIndex;
        // Te same slajdy (np. zmiana kolejności planu) = ta sama migawka — bez ponownego
        // przejścia (CrossFade) na rzutniku. Zmieniony układ części albo poprawiony tekst — podmiana.
        if (current?.PlanItemId is { } id && playlist.FirstOrDefault(i => i.PlanItemId == id) is { } updated)
        {
            if (!SameSlides(current.Slides, updated.Slides))
            {
                slideIndex = MapSlide(current.Slides, updated.Slides, slideIndex);
                current = updated;
            }
            else if (current.Note != updated.Note)
            {
                // np. prezentacja bez slajdów: „Przygotowywanie…” → komunikat błędu (slajdy te same — bez przejścia)
                current = current with { Note = updated.Note };
            }
        }
        Publish(current, slideIndex, State.IsBlank);
    }

    /// <summary>
    /// Odpowiednik slajdu <paramref name="index"/> po zmianie układu części: najpierw to samo wystąpienie
    /// tej samej części (np. drugi refren — działa też po zmianie kolejności i powtórzeniu), a gdy go już nie ma,
    /// najbliższy zachowany slajd (krótszy układ jest wtedy podciągiem dłuższego).
    /// </summary>
    private static int MapSlide(IReadOnlyList<Slide> before, IReadOnlyList<Slide> after, int index)
    {
        if (index < before.Count)
        {
            var occurrence = before.Take(index).Count(s => SameSlide(s, before[index]));
            var match = after.Select((s, i) => (s, i)).Where(x => SameSlide(x.s, before[index])).Skip(occurrence).FirstOrDefault();
            if (match.s is not null)
                return match.i;
        }

        var map = new int?[before.Count];
        if (after.Count <= before.Count)
        {
            var j = 0;
            for (var i = 0; i < before.Count && j < after.Count; i++)
                if (SameSlide(before[i], after[j]))
                    map[i] = j++;
        }
        else
        {
            var i = 0;
            for (var j = 0; j < after.Count && i < before.Count; j++)
                if (SameSlide(before[i], after[j]))
                    map[i++] = j;
        }

        if (index < map.Length && map[index] is { } same)
            return same;
        for (var i = index + 1; i < map.Length; i++)
            if (map[i] is { } following)
                return following;
        for (var i = Math.Min(index, map.Length) - 1; i >= 0; i--)
            if (map[i] is { } preceding)
                return preceding;
        return 0;
    }

    /// <summary>Ten sam slajd po zmianie układu — także zwrotka, która straciła albo zyskała dołączony refren.</summary>
    private static bool SameSlide(Slide a, Slide b) => a.SectionCode == b.SectionCode && a.FirstPartLabel == b.FirstPartLabel;

    private static readonly IReadOnlyList<SlideLine> NoLines = [];

    /// <summary>Ta sama treść: wersy i pozostałe pola (także slajdów innych rodzajów niż tekst).</summary>
    private static bool SameSlides(IReadOnlyList<Slide> a, IReadOnlyList<Slide> b) =>
        a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First with { Lines = NoLines } == pair.Second with { Lines = NoLines } &&
            pair.First.Lines.SequenceEqual(pair.Second.Lines));

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
        if (index < 0)
            return null;
        // Pieśni ze wszystkimi częściami pominiętymi nie ma czego pokazać — przeskakujemy je.
        for (var target = index + step; target >= 0 && target < _playlist.Count; target += step)
            if (_playlist[target].Slides.Count > 0)
                return _playlist[target];
        return null;
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
