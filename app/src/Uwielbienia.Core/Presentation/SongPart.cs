using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

/// <summary>Jedno wystąpienie części pieśni w układzie planu (kolumna „Pieśń”, panel w przeglądarce).</summary>
/// <param name="Lines">Wersy z akordami, jak na slajdach (dziedziczenie akordów już zastosowane).</param>
/// <param name="Sung">Część jest śpiewana; odznaczona = pominięta w tym planie.</param>
public sealed record SongPart(string Code, string Name, IReadOnlyList<SlideLine> Lines, bool Sung)
{
    public static SongPart For(ISlideBuilder slides, Song song, LayoutEntry entry) => new(
        entry.Code,
        song.FindSection(entry.Code)?.Name ?? entry.Code,
        slides.Build(song, [entry.Code]).SelectMany(s => s.Lines).ToList(),
        entry.Sung);
}
