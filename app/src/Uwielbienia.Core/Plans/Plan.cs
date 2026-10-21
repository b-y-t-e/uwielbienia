using System.Text.Json.Serialization;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Plans;

public enum PlanKind
{
    /// <summary>Konkretne wydarzenie, np. „Uwielbienie 25 września 2026”.</summary>
    Event,
    /// <summary>Szablon do wielokrotnego użycia, np. „Próba z dziećmi”.</summary>
    Template,
}

/// <summary>Plan (lista pozycji) na wydarzenie albo szablon.</summary>
/// <param name="Transitions">
/// Efekty przejścia z jednej pozycji planu na inną (rzutnik; kiosk ma własne — <see cref="KioskPlanItem.Transitions"/>) —
/// przy kilku losowany jest jeden; pusta lista = zwykłe przenikanie; <c>null</c> = domyślne (<see cref="DefaultTransitions"/>).
/// </param>
/// <param name="InnerTransitions">
/// Efekty przejścia między slajdami jednej pozycji (zwrotki pieśni, slajdy prezentacji); <c>null</c> = jak między pozycjami.
/// </param>
public sealed record Plan(
    Guid Id,
    string Name,
    DateOnly? Date,
    PlanKind Kind,
    IReadOnlyList<PlanItem> Items,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SlideTransition>? Transitions = null,
    IReadOnlyList<SlideTransition>? InnerTransitions = null)
{
    public static IReadOnlyList<SlideTransition> DefaultTransitions => SlideTransitions.Default;

    public IReadOnlyList<SlideTransition> ChosenTransitions => Transitions ?? DefaultTransitions;

    public IReadOnlyList<SlideTransition> ChosenInnerTransitions => InnerTransitions ?? ChosenTransitions;

    /// <summary>Przejście między slajdami pozycji: własne efekty albo <c>null</c> = jak między pozycjami.</summary>
    public Plan WithInnerTransitions(IReadOnlyList<SlideTransition>? chosen) =>
        this with { InnerTransitions = chosen, UpdatedAt = DateTimeOffset.Now };

    /// <summary>Nowy wybór efektów przejścia (równy domyślnemu — plan podąża za domyślnymi).</summary>
    public Plan WithTransitions(IReadOnlyList<SlideTransition> chosen) =>
        this with { Transitions = SlideTransitions.ToStored(chosen), UpdatedAt = DateTimeOffset.Now };

    public static Plan Create(string name, DateOnly? date, PlanKind kind = PlanKind.Event) =>
        new(Guid.NewGuid(), name, date, kind, [], DateTimeOffset.Now);

    /// <summary>Kopia z nowymi identyfikatorami — „Użyj ponownie” albo „Zapisz jako szablon”.</summary>
    public Plan CloneAs(string name, DateOnly? date, PlanKind kind) =>
        new(Guid.NewGuid(), name, date, kind, Items.Select(i => i.WithNewId()).ToList(), DateTimeOffset.Now, Transitions, InnerTransitions);

    public Plan WithItems(IReadOnlyList<PlanItem> items) => this with { Items = items, UpdatedAt = DateTimeOffset.Now };

    public Plan Insert(int index, PlanItem item)
    {
        var items = Items.ToList();
        items.Insert(Math.Clamp(index, 0, items.Count), item);
        return WithItems(items);
    }

    /// <summary>Wstawia zaraz za pozycją <paramref name="itemId"/> (np. tą na ekranie), a gdy jej nie ma — na koniec.</summary>
    public Plan InsertAfter(Guid? itemId, PlanItem item) =>
        Insert(itemId is { } id && IndexOf(id) is >= 0 and var index ? index + 1 : Items.Count, item);

    public Plan Remove(Guid itemId) => WithItems(Items.Where(i => i.Id != itemId).ToList());

    public Plan Replace(PlanItem item) => WithItems(Items.Select(i => i.Id == item.Id ? item : i).ToList());

    public Plan Move(Guid itemId, int newIndex)
    {
        var items = Items.ToList();
        var item = items.Single(i => i.Id == itemId);
        items.Remove(item);
        items.Insert(Math.Clamp(newIndex, 0, items.Count), item);
        return WithItems(items);
    }

    public int IndexOf(Guid itemId) => Items.ToList().FindIndex(i => i.Id == itemId);
}

/// <summary>
/// Pozycja planu: pieśń lub tekst (<see cref="SongPlanItem"/>), prezentacja (<see cref="PresentationPlanItem"/>),
/// film (<see cref="VideoPlanItem"/>), kiosk (<see cref="KioskPlanItem"/>).
/// Nowy rodzaj = nowy rekord z <see cref="JsonDerivedTypeAttribute"/> (CLAUDE.md: „Nowy rodzaj elementu”).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SongPlanItem), "song")]
[JsonDerivedType(typeof(PresentationPlanItem), "presentation")]
[JsonDerivedType(typeof(VideoPlanItem), "video")]
[JsonDerivedType(typeof(KioskPlanItem), "kiosk")]
public abstract record PlanItem(Guid Id)
{
    public abstract PlanItem WithNewId();

    /// <summary>Własne efekty przejścia na tę pozycję z innej pozycji planu; <c>null</c> = przejście planu.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<SlideTransition>? EntryTransitions => null;

    /// <summary>Własne efekty przejścia między slajdami tej pozycji; <c>null</c> = przejście slajdów planu.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<SlideTransition>? InnerTransitions => null;
}

/// <summary>Część w układzie pieśni: kod sekcji i czy jest śpiewana (odznaczona = pominięta).</summary>
public sealed record LayoutEntry(string Code, bool Sung = true);

/// <summary>Pieśń w planie z wybranymi częściami w wybranej kolejności.</summary>
/// <param name="Arrangement">Kody sekcji do zaśpiewania; <c>null</c> = kolejność z pliku pieśni.</param>
/// <param name="Layout">
/// Pełny układ pieśni w tym planie (kolejność, powtórzenia, części pominięte); <c>null</c> = wynika z
/// <paramref name="Arrangement"/> i kolejności z pliku. <paramref name="Arrangement"/> to zawsze jego śpiewana część.
/// </param>
/// <param name="Removed">
/// Części usunięte z układu w tym planie („Usuń”). Odróżnia je od części dopisanych później w edytorze
/// (albo w nowej wersji śpiewnika), które pojawiają się w układzie same. <c>null</c> = plan sprzed tego pola.
/// </param>
/// <param name="Transitions">
/// Własne efekty przejścia na tę pieśń z innej pozycji planu (jak <see cref="Plan.Transitions"/>: pusta lista = przenikanie,
/// kilka = losowo jeden); <c>null</c> = przejście planu.
/// </param>
public sealed record SongPlanItem(
    Guid Id,
    string SongId,
    IReadOnlyList<string>? Arrangement,
    IReadOnlyList<LayoutEntry>? Layout = null,
    IReadOnlyList<string>? Removed = null,
    IReadOnlyList<SlideTransition>? Transitions = null)
    : PlanItem(Id)
{
    [JsonIgnore]
    public override IReadOnlyList<SlideTransition>? EntryTransitions => Transitions;

    /// <summary>
    /// Układ do wyświetlenia w kolumnie „Pieśń”, uzgodniony z aktualną treścią pieśni: bez części, których już nie
    /// ma, z nowymi częściami (śpiewanymi, na końcu), które nie zostały usunięte w planie.
    /// Tekst ma zawsze kolejność z edytora — plan pamięta tylko pominięte i usunięte części.
    /// </summary>
    public IReadOnlyList<LayoutEntry> LayoutFor(Song song)
    {
        if (Layout is null)
        {
            if (Arrangement is null)
                return song.Arrangement.Select(code => new LayoutEntry(code)).ToList();
            // Dawniej zapisywano tylko śpiewane części w kolejności pliku — pozostałe są pominięte.
            var remaining = Arrangement.ToList();
            return song.Arrangement.Select(code => new LayoutEntry(code, remaining.Remove(code))).ToList();
        }

        var existing = song.Sections.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        var removed = (Removed ?? []).ToHashSet(StringComparer.Ordinal);
        var layout = Layout.Where(e => existing.Contains(e.Code)).ToList();
        if (song.IsText)
        {
            var sung = layout.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First().Sung, StringComparer.Ordinal);
            return song.Arrangement.Distinct().Where(code => !removed.Contains(code))
                .Select(code => new LayoutEntry(code, sung.GetValueOrDefault(code, true))).ToList();
        }

        // nowe części na końcu układu (jak „Przywróć usuniętą część”), w kolejności z pliku
        var known = layout.Select(e => e.Code).Concat(removed).ToHashSet(StringComparer.Ordinal);
        layout.AddRange(song.Arrangement.Where(known.Add).Select(code => new LayoutEntry(code)));
        return layout;
    }

    /// <summary>Kody do zaśpiewania (ekran): śpiewana część uzgodnionego układu.</summary>
    public IReadOnlyList<string> ArrangementFor(Song song) =>
        Layout is null && Arrangement is null
            ? song.Arrangement
            : LayoutFor(song).Where(e => e.Sung).Select(e => e.Code).ToList();

    /// <summary>
    /// Nowy układ; części pieśni, których w nim nie ma, są usunięte w tym planie. Gdy układ jest taki jak w pliku
    /// pieśni, pozycja znowu podąża za plikiem.
    /// </summary>
    public SongPlanItem WithLayout(Song song, IReadOnlyList<LayoutEntry> layout)
    {
        var removed = song.Arrangement.Distinct().Where(code => layout.All(e => e.Code != code)).ToList();
        var isDefault = removed.Count == 0 && layout.All(e => e.Sung)
                        && (song.IsText || layout.Select(e => e.Code).SequenceEqual(song.Arrangement));
        return isDefault
            ? this with { Arrangement = null, Layout = null, Removed = null }
            : this with { Arrangement = layout.Where(e => e.Sung).Select(e => e.Code).ToList(), Layout = layout, Removed = removed };
    }

    public static SongPlanItem For(string songId) => new(Guid.NewGuid(), songId, null);

    public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };
}

/// <summary>Prezentacja (PowerPoint albo obrazy) skopiowana do folderu aplikacji.</summary>
/// <param name="Folder">Folder prezentacji w katalogu aplikacji (<see cref="Slideshows.ISlideshowLibrary"/>).</param>
/// <param name="Transitions">Własne efekty przejścia na prezentację z innej pozycji planu; <c>null</c> = jak w planie.</param>
/// <param name="SlideEffects">
/// Efekty przejścia między slajdami prezentacji — niezależne od przejścia slajdów planu; <c>null</c> albo pusta lista =
/// przenikanie (jak w PowerPoincie).
/// </param>
public sealed record PresentationPlanItem(
    Guid Id,
    string Folder,
    string Title,
    IReadOnlyList<SlideTransition>? Transitions = null,
    IReadOnlyList<SlideTransition>? SlideEffects = null) : PlanItem(Id)
{
    public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };

    [JsonIgnore]
    public override IReadOnlyList<SlideTransition>? EntryTransitions => Transitions;

    [JsonIgnore]
    public override IReadOnlyList<SlideTransition>? InnerTransitions => SlideEffects ?? [];
}

/// <summary>Film wskazany na dysku (nie jest kopiowany — pliki filmów bywają bardzo duże).</summary>
/// <param name="File">Pełna ścieżka pliku filmu.</param>
public sealed record VideoPlanItem(Guid Id, string File, string Title) : PlanItem(Id)
{
    public static VideoPlanItem For(string file) => new(Guid.NewGuid(), file, Path.GetFileNameWithoutExtension(file));

    public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };
}

/// <summary>
/// Kiosk: obrazy i napisy, które zmieniają się same co <see cref="Seconds"/> sekund (w kółko). Dla planu i pilota to jeden
/// slajd — „Dalej” przechodzi do następnej pozycji planu.
/// </summary>
/// <param name="Seconds">Co ile sekund następny element.</param>
/// <param name="Transitions">
/// Efekty przejścia między elementami — przy kilku losowany jest jeden; pusta lista = zwykłe przenikanie;
/// <c>null</c> = domyślne (<see cref="DefaultTransitions"/>). Niezależne od przejść planu.
/// </param>
/// <param name="SlowZoom">Zdjęcia powoli przybliżają się i przesuwają, póki są na ekranie.</param>
public sealed record KioskPlanItem(
    Guid Id,
    string Title,
    IReadOnlyList<KioskEntry> Entries,
    int Seconds = KioskPlanItem.DefaultSeconds,
    IReadOnlyList<SlideTransition>? Transitions = null,
    bool SlowZoom = true)
    : PlanItem(Id)
{
    public const int DefaultSeconds = 8;
    public const int MinSeconds = 2;
    public const int MaxSeconds = 600;

    public static IReadOnlyList<SlideTransition> DefaultTransitions => SlideTransitions.Default;

    public IReadOnlyList<SlideTransition> ChosenTransitions => Transitions ?? DefaultTransitions;

    public static KioskPlanItem Create(string title = "Kiosk") => new(Guid.NewGuid(), title, []);

    // Foldery obrazów się nie zmieniają (edycja kiosku tylko dodaje nowe) — kopia może je współdzielić.
    public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };
}

/// <summary>Element kiosku: obraz (albo wszystkie slajdy prezentacji), napis, albo obraz z napisem.</summary>
/// <param name="Folder">Folder obrazu lub prezentacji w katalogu aplikacji (<see cref="Slideshows.ISlideshowLibrary"/>); <c>null</c> = sam napis.</param>
/// <param name="Title">Tytuł napisu (opcjonalny).</param>
/// <param name="Text">Treść napisu; wiersze rozdziela znak nowej linii.</param>
public sealed record KioskEntry(string? Folder = null, string? Title = null, string? Text = null)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Text);
}
