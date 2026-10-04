using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

/// <summary>Coś, co można wyświetlić: pozycja planu (pieśń, tekst, …) albo pieśń dobrana „na szybko”.</summary>
/// <param name="PlanItemId">Pozycja planu, z której pochodzi; <c>null</c> = spoza planu.</param>
/// <param name="ContentId">Identyfikator treści, np. pieśni (<c>Song.Id</c>).</param>
/// <param name="Number">Numer w śpiewniku; <c>null</c> = bez numeru.</param>
/// <param name="Title">Tytuł albo (tekst bez tytułu) pierwszy wers.</param>
/// <param name="HasOwnTitle">Ma własny tytuł — tylko wtedy rzutnik pokazuje go nad pierwszym slajdem.</param>
public sealed record LiveItem(Guid? PlanItemId, string ContentId, int? Number, string Title, IReadOnlyList<Slide> Slides, bool HasOwnTitle = true);

/// <summary>Pieśń spoza planu na ekran (szybki wybór na projekcji, telefon).</summary>
public interface ISongPresenter
{
    LiveItem Present(Song song);
}

/// <summary>Rodzaj pozycji „pieśń” (także tekst — ten sam model <see cref="Song"/>).</summary>
public sealed class SongPlanItemType(ISongLibrary library, ISlideBuilder slideBuilder) : IPlanItemType, ISongPresenter
{
    public bool Handles(PlanItem item) => item is SongPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is SongPlanItem songItem && library.Find(songItem.SongId) is { } song
            ? new PlanItemInfo(song.DisplayTitle, song.Number, song.IsText ? PlanItemKind.Text : PlanItemKind.Song)
            : null;

    public LiveItem? Present(PlanItem item) =>
        item is SongPlanItem songItem && library.Find(songItem.SongId) is { } song
            ? Create(item.Id, song, songItem.Arrangement ?? song.Arrangement)
            : null;

    public LiveItem Present(Song song) => Create(null, song, song.Arrangement);

    private LiveItem Create(Guid? planItemId, Song song, IReadOnlyList<string> arrangement) =>
        new(planItemId, song.Id, song.Number, song.DisplayTitle, slideBuilder.Build(song, arrangement), song.Title.Length > 0);
}
