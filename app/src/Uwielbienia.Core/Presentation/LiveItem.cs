using System.Text.RegularExpressions;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

/// <summary>Coś, co można wyświetlić: pozycja planu (pieśń, tekst, …) albo pieśń dobrana „na szybko”.</summary>
/// <param name="PlanItemId">Pozycja planu, z której pochodzi; <c>null</c> = spoza planu.</param>
/// <param name="ContentId">Identyfikator treści, np. pieśni (<c>Song.Id</c>).</param>
/// <param name="Number">Numer w śpiewniku; <c>null</c> = bez numeru.</param>
/// <param name="Title">Tytuł albo (tekst bez tytułu) pierwszy wers.</param>
/// <param name="ShowTitle">
/// Rzutnik pokazuje tytuł nad pierwszym slajdem — tylko własny tytuł, który nie powtarza początku pierwszego wersu.
/// </param>
/// <param name="Note">Dla operatora: dlaczego nie ma slajdów (np. „Przygotowywanie slajdów…”).</param>
/// <param name="Transitions">
/// Efekty przejścia na tę pozycję z innej (z planu — <see cref="Plans.Plan.Transitions"/>); pusta lista albo <c>null</c>
/// (spoza planu) = zwykłe krótkie przenikanie.
/// </param>
/// <param name="InnerTransitions">
/// Efekty przejścia między slajdami tej pozycji (z planu — <see cref="Plans.Plan.InnerTransitions"/>); jak <paramref name="Transitions"/>.
/// </param>
public sealed record LiveItem(
    Guid? PlanItemId,
    string ContentId,
    int? Number,
    string Title,
    IReadOnlyList<Slide> Slides,
    bool ShowTitle = true,
    string? Note = null,
    IReadOnlyList<SlideTransition>? Transitions = null,
    IReadOnlyList<SlideTransition>? InnerTransitions = null);

/// <summary>Pieśń spoza planu na ekran (szybki wybór na projekcji, telefon).</summary>
public interface ISongPresenter
{
    LiveItem Present(Song song);
}

/// <summary>Rodzaj pozycji „pieśń” (także tekst — ten sam model <see cref="Song"/>).</summary>
public sealed partial class SongPlanItemType : IPlanItemType, ISongPresenter
{
    private readonly ISongLibrary _library;
    private readonly ISlideBuilder _slideBuilder;

    public SongPlanItemType(ISongLibrary library, ISlideBuilder slideBuilder)
    {
        _library = library;
        _slideBuilder = slideBuilder;
        // Zmieniona pieśń (edycja) trafia od razu do planu i — jeśli jest grana — na ekran.
        library.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public bool Handles(PlanItem item) => item is SongPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is SongPlanItem songItem && _library.Find(songItem.SongId) is { } song
            ? new PlanItemInfo(song.DisplayTitle, song.Number, song.IsText ? PlanItemKind.Text : PlanItemKind.Song)
            : null;

    public LiveItem? Present(PlanItem item) =>
        item is SongPlanItem songItem && _library.Find(songItem.SongId) is { } song
            ? Create(item.Id, song, songItem.ArrangementFor(song))
            : null;

    public LiveItem Present(Song song) => Create(null, song, song.Arrangement);

    /// <summary>Kod slajdu z samym tytułem (tekst bez treści) — nie koliduje z kodami części.</summary>
    public const string TitleSlideCode = "TYTUL";

    private LiveItem Create(Guid? planItemId, Song song, IReadOnlyList<string> arrangement)
    {
        var slides = _slideBuilder.Build(song, arrangement);
        // tekst z samym tytułem: jeden slajd, na którym jest tylko tytuł (w stylu tytułu, z linią)
        if (slides.Count == 0 && song.IsText && song.Sections.Count == 0 && song.Title.Length > 0)
            slides = [new Slide(TitleSlideCode, "Tytuł", [])];
        var titleSpans = TitleSpans(song.Title, slides);
        if (titleSpans.Count > 0)
            slides = [MarkTitle(slides[0], titleSpans), .. slides.Skip(1)];
        var showTitle = song.Title.Length > 0 && titleSpans.Count == 0;
        return new(planItemId, song.Id, song.Number, song.DisplayTitle, slides, showTitle, "Wszystkie części pominięte");
    }

    /// <summary>
    /// Ile znaków kolejnych wersów pierwszego slajdu powtarza tytuł — po jednej liczbie na wers, który go zawiera
    /// (np. tytuł „Uwielbienie jest” i wers „Uwielbienie jest piękne” → [16]; „Jezus / pokonał śmierć, …” →
    /// [5, 14]); pusta lista = tytuł nie jest początkiem tekstu. Wtedy rzutnik pokazuje tytuł nad tekstem,
    /// a inaczej — zamiast tytułu — te słowa w kolorze tytułu. Porównanie bez wielkości liter, polskich znaków
    /// i interpunkcji, całymi słowami („Jezus” nie jest początkiem „Jezusie mój”).
    /// </summary>
    internal static IReadOnlyList<int> TitleSpans(string title, IReadOnlyList<Slide> slides)
    {
        var titleWords = SongSearch.Normalize(title).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (titleWords.Length == 0 || slides.Count == 0)
            return [];
        var spans = new List<int>();
        var matched = 0;
        foreach (var line in slides[0].Lines)
        {
            foreach (Match token in Token().Matches(line.Text))
                foreach (var word in SongSearch.Normalize(token.Value).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word != titleWords[matched])
                        return [];
                    if (++matched == titleWords.Length)
                    {
                        spans.Add(token.Index + token.Length);
                        return spans;
                    }
                }
            spans.Add(line.Text.Length);
        }
        return [];
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Token();

    private static Slide MarkTitle(Slide slide, IReadOnlyList<int> spans) =>
        slide with { Lines = [.. slide.Lines.Select((line, i) => i < spans.Count ? line with { TitleLength = spans[i] } : line)] };
}

/// <summary>Rodzaj pozycji „prezentacja”: slajdy-obrazy przygotowane przez <see cref="ISlideshowLibrary"/>.</summary>
public sealed class PresentationPlanItemType : IPlanItemType
{
    private readonly ISlideshowLibrary _library;

    public PresentationPlanItemType(ISlideshowLibrary library)
    {
        _library = library;
        library.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public bool Handles(PlanItem item) => item is PresentationPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is PresentationPlanItem presentation
            ? new PlanItemInfo(presentation.Title, null, PlanItemKind.Presentation, _library.Peek(presentation.Folder).Note)
            : null;

    public LiveItem? Present(PlanItem item)
    {
        if (item is not PresentationPlanItem presentation)
            return null;
        var state = _library.Load(presentation.Folder);
        var slides = state.Slides.Select((file, i) => (Slide)new ImageSlide(file, i + 1)).ToList();
        return new LiveItem(item.Id, presentation.Folder, null, presentation.Title, slides, ShowTitle: false, state.Note);
    }
}
