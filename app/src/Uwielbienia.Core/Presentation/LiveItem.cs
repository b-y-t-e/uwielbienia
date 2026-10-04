using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

/// <summary>Coś, co można wyświetlić: pozycja planu (pieśń, tekst, …) albo pieśń dobrana „na szybko”.</summary>
/// <param name="PlanItemId">Pozycja planu, z której pochodzi; <c>null</c> = spoza planu.</param>
/// <param name="ContentId">Identyfikator treści, np. pieśni (<c>Song.Id</c>).</param>
/// <param name="Number">Numer w śpiewniku; <c>null</c> = bez numeru.</param>
/// <param name="Title">Tytuł albo (tekst bez tytułu) pierwszy wers.</param>
/// <param name="HasOwnTitle">Ma własny tytuł — tylko wtedy rzutnik pokazuje go nad pierwszym slajdem.</param>
/// <param name="Note">Dla operatora: dlaczego nie ma slajdów (np. „Przygotowywanie slajdów…”).</param>
public sealed record LiveItem(
    Guid? PlanItemId,
    string ContentId,
    int? Number,
    string Title,
    IReadOnlyList<Slide> Slides,
    bool HasOwnTitle = true,
    string? Note = null);

/// <summary>Pieśń spoza planu na ekran (szybki wybór na projekcji, telefon).</summary>
public interface ISongPresenter
{
    LiveItem Present(Song song);
}

/// <summary>Rodzaj pozycji „pieśń” (także tekst — ten sam model <see cref="Song"/>).</summary>
public sealed class SongPlanItemType : IPlanItemType, ISongPresenter
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
            ? Create(item.Id, song, songItem.Arrangement ?? song.Arrangement)
            : null;

    public LiveItem Present(Song song) => Create(null, song, song.Arrangement);

    private LiveItem Create(Guid? planItemId, Song song, IReadOnlyList<string> arrangement) =>
        new(planItemId, song.Id, song.Number, song.DisplayTitle, _slideBuilder.Build(song, arrangement), song.Title.Length > 0,
            "Wszystkie części pominięte");
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
        return new LiveItem(item.Id, presentation.Folder, null, presentation.Title, slides, HasOwnTitle: false, state.Note);
    }
}
