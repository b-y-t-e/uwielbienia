using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

public interface ISlideBuilder
{
    /// <summary>Slajdy pieśni w kolejności wykonania (każde wystąpienie sekcji w <paramref name="arrangement"/>).</summary>
    IReadOnlyList<Slide> Build(Song song, IReadOnlyList<string> arrangement);
}

/// <summary>
/// Jedna sekcja = jeden slajd; sekcje dłuższe niż limit dzielone są na równe części. Krótka zwrotka
/// i refren zaraz po niej trafiają na jeden slajd (<paramref name="joinVerseAndChorus"/>), gdy razem mieszczą
/// się na ekranie (najwyżej lekko zmniejszone) — mniej przełączania w pieśniach o dwuwersowych częściach.
/// </summary>
public sealed class SectionSlideBuilder(int maxLinesPerSlide = 6, bool joinVerseAndChorus = true) : ISlideBuilder
{
    /// <summary>
    /// Najwięcej wierszy połączonego slajdu (płótno 1920×1080, Literata 86 px, marginesy 40 px u góry i u dołu):
    /// 8 mieści się w pełnej wielkości, przy 9 tekst zmniejsza się o ok. 12%.
    /// </summary>
    internal const int ScreenLines = 9;

    /// <summary>Szacunkowa liczba znaków w wierszu rzutnika — dłuższy wers zawija się na kolejny wiersz.</summary>
    internal const int LineChars = 40;

    public IReadOnlyList<Slide> Build(Song song, IReadOnlyList<string> arrangement)
    {
        var slides = new List<Slide>();
        var lastWasWholeVerse = false;
        foreach (var code in arrangement)
        {
            var section = song.FindSection(code);
            if (section is null)
                continue;

            var chords = ChordResolver.EffectiveChords(song, section);
            var lines = section.Lines
                .Select((line, i) => (line, chords: chords[i]))
                .Where(x => x.line.IsSung)
                .Select(x => new SlideLine(x.line.Text, x.chords, x.line.Repeat, x.line.RepeatStart, x.line.RepeatEnd))
                .ToList();
            if (lines.Count == 0)
                continue;

            var parts = Split(lines);
            var whole = parts.Count == 1;
            if (joinVerseAndChorus && !song.IsText && whole && lastWasWholeVerse && section.Kind == "C" &&
                ScreenRows(slides[^1].Lines) + ScreenRows(lines) <= ScreenLines)
            {
                slides[^1] = Join(slides[^1], section.Name, lines);
                lastWasWholeVerse = false;
                continue;
            }

            for (var p = 0; p < parts.Count; p++)
            {
                var label = whole ? section.Name : $"{section.Name} ({p + 1}/{parts.Count})";
                slides.Add(new Slide(section.Code, label, parts[p]));
            }
            lastWasWholeVerse = whole && section.Kind == "V";
        }
        return slides;
    }

    /// <summary>
    /// Zwrotka + refren na jednym slajdzie: kod i początek etykiety zwrotki (dopasowanie po zmianie układu),
    /// refren oznaczony (<see cref="SlideLine.IsChorus"/>), żeby rzutnik oddzielił go odstępem i kursywą.
    /// </summary>
    private static Slide Join(Slide verse, string chorusName, IReadOnlyList<SlideLine> chorus) =>
        new(verse.SectionCode, verse.Label + Slide.JoinSeparator + chorusName,
            [.. verse.Lines, .. chorus.Select((line, i) => line with { IsChorus = true, StartsPart = i == 0 })]);

    /// <summary>Wiersze na rzutniku z zawijaniem długich wersów („ ×2” to ok. 3 znaki).</summary>
    private static int ScreenRows(IEnumerable<SlideLine> lines) =>
        lines.Sum(l => Math.Max(1, (int)Math.Ceiling((l.Text.Length + (l.Repeat > 1 ? 3 : 0)) / (double)LineChars)));

    private List<IReadOnlyList<SlideLine>> Split(List<SlideLine> lines)
    {
        if (lines.Count <= maxLinesPerSlide)
            return [lines];

        var partCount = (int)Math.Ceiling(lines.Count / (double)maxLinesPerSlide);
        var baseSize = lines.Count / partCount;
        var remainder = lines.Count % partCount;
        var parts = new List<IReadOnlyList<SlideLine>>(partCount);
        var start = 0;
        for (var p = 0; p < partCount; p++)
        {
            var size = baseSize + (p < remainder ? 1 : 0);
            parts.Add(lines.GetRange(start, size));
            start += size;
        }
        return parts;
    }
}
