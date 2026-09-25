namespace Uwielbienia.Core.Songs;

/// <summary>
/// Dziedziczenie akordów (CLAUDE.md): sekcja bez żadnych akordów przejmuje je wers po wersie
/// z pierwszej sekcji tego samego rodzaju, która je ma.
/// </summary>
public static class ChordResolver
{
    /// <summary>Akordy do wyświetlenia dla każdego wiersza sekcji (ta sama długość co <c>section.Lines</c>).</summary>
    public static IReadOnlyList<string?> EffectiveChords(Song song, Section section)
    {
        if (section.HasChords)
            return section.Lines.Select(l => l.Chords).ToList();

        var pattern = song.Sections.FirstOrDefault(s => s.Kind == section.Kind && s.HasChords);
        if (pattern is null)
            return section.Lines.Select(_ => (string?)null).ToList();

        var patternLyrics = pattern.Lines.Where(l => l.Kind != LineKind.Note).ToList();
        var result = new List<string?>(section.Lines.Count);
        var index = 0;
        foreach (var line in section.Lines)
        {
            if (line.Kind == LineKind.Note)
            {
                result.Add(null);
                continue;
            }
            result.Add(index < patternLyrics.Count ? patternLyrics[index].Chords : null);
            index++;
        }
        return result;
    }
}
