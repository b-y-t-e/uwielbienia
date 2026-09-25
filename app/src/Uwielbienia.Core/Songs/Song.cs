namespace Uwielbienia.Core.Songs;

/// <summary>Pieśń odczytana z pliku <c>piesn.md</c> (format opisany w CLAUDE.md).</summary>
/// <param name="Id">Nazwa folderu (<c>NNN-slug</c>) — unikalna, bo PDF ma zdublowane numery.</param>
public sealed record Song(
    string Id,
    int Number,
    string Title,
    string Category,
    int? SourceNumber,
    string? Key,
    IReadOnlyList<string> Arrangement,
    IReadOnlyList<Section> Sections)
{
    public Section? FindSection(string code) =>
        Sections.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.Ordinal));

    /// <summary>Pierwszy śpiewany wers — pomocny w wyszukiwaniu i na liście.</summary>
    public string FirstLine =>
        Arrangement.Select(FindSection).OfType<Section>()
            .SelectMany(s => s.Lines).FirstOrDefault(l => l.IsSung)?.Text ?? Title;
}

/// <param name="Code">Kod sekcji, np. <c>V1</c>, <c>C</c>, <c>B</c>.</param>
/// <param name="Repeat">Ile razy śpiewa się całą sekcję (<c>{xN}</c> w nagłówku).</param>
public sealed record Section(string Code, string Name, int Repeat, IReadOnlyList<SongLine> Lines)
{
    public bool HasChords => Lines.Any(l => l.Chords is not null);

    /// <summary>Rodzaj sekcji bez numeru: <c>V2</c> → <c>V</c>, <c>C</c> → <c>C</c>.</summary>
    public string Kind => Code.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
}

/// <summary>Jeden wiersz sekcji.</summary>
/// <param name="Text">Tekst bez znaczników powtórzeń i akordów.</param>
/// <param name="Chords">Akordy wersu albo <c>null</c>.</param>
/// <param name="Repeat">Ile razy śpiewa się wers lub fragment zakończony tym wersem.</param>
/// <param name="RepeatStart">Wers otwiera fragment <c>|: … :|</c>.</param>
/// <param name="RepeatEnd">Wers zamyka fragment <c>|: … :|</c>.</param>
public sealed record SongLine(
    string Text,
    string? Chords,
    int Repeat,
    bool RepeatStart,
    bool RepeatEnd,
    LineKind Kind)
{
    public bool IsSung => Kind == LineKind.Lyric;
}

public enum LineKind
{
    Lyric,
    /// <summary>Wiersz zawierający wyłącznie akordy.</summary>
    Instrumental,
    /// <summary>Uwaga wykonawcza (<c>&gt; …</c>) — nie wyświetlana na ekranie.</summary>
    Note,
}
