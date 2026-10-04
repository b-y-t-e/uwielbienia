using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

/// <summary>Coś, co można wyświetlić: pieśń z planu albo dobrana „na szybko”.</summary>
/// <param name="PlanItemId">Pozycja planu, z której pochodzi; <c>null</c> = spoza planu.</param>
/// <param name="Number">Numer w śpiewniku; <c>null</c> dla tekstu.</param>
/// <param name="Title">Tytuł albo (tekst bez tytułu) pierwszy wers.</param>
/// <param name="HasOwnTitle">Ma własny tytuł — tylko wtedy rzutnik pokazuje go nad pierwszym slajdem.</param>
public sealed record LiveItem(Guid? PlanItemId, string SongId, int? Number, string Title, IReadOnlyList<Slide> Slides, bool HasOwnTitle = true);

public interface ILiveItemFactory
{
    LiveItem? Create(SongPlanItem item);

    LiveItem Create(Song song);
}

public sealed class LiveItemFactory(ISongLibrary library, ISlideBuilder slideBuilder) : ILiveItemFactory
{
    public LiveItem? Create(SongPlanItem item)
    {
        var song = library.Find(item.SongId);
        return song is null
            ? null
            : new LiveItem(item.Id, song.Id, song.Number, song.DisplayTitle, slideBuilder.Build(song, item.Arrangement ?? song.Arrangement),
                song.Title.Length > 0);
    }

    public LiveItem Create(Song song) =>
        new(null, song.Id, song.Number, song.DisplayTitle, slideBuilder.Build(song, song.Arrangement), song.Title.Length > 0);
}
