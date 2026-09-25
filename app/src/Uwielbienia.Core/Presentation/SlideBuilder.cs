using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Presentation;

public interface ISlideBuilder
{
    /// <summary>Slajdy pieśni w kolejności wykonania (każde wystąpienie sekcji w <paramref name="arrangement"/>).</summary>
    IReadOnlyList<Slide> Build(Song song, IReadOnlyList<string> arrangement);
}

/// <summary>Jedna sekcja = jeden slajd; sekcje dłuższe niż limit dzielone są na równe części.</summary>
public sealed class SectionSlideBuilder(int maxLinesPerSlide = 6) : ISlideBuilder
{
    public IReadOnlyList<Slide> Build(Song song, IReadOnlyList<string> arrangement)
    {
        var slides = new List<Slide>();
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
            for (var p = 0; p < parts.Count; p++)
            {
                var label = parts.Count == 1 ? section.Name : $"{section.Name} ({p + 1}/{parts.Count})";
                slides.Add(new Slide(section.Code, label, parts[p]));
            }
        }
        return slides;
    }

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
