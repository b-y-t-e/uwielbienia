using System.Globalization;
using System.Text.RegularExpressions;

namespace Uwielbienia.Core.Songs;

/// <summary>Parser formatu <c>piesn.md</c> — reguły parsowania z CLAUDE.md.</summary>
public sealed partial class MarkdownSongParser : ISongParser
{
    [GeneratedRegex(@"^## \[(?<kod>[A-Z]+\d*)\] (?<nazwa>.+?)(?: \{x(?<n>\d+)\})?$")]
    private static partial Regex SectionHeader();

    [GeneratedRegex(@"\s*`(?<akordy>[^`]*)`\s*$")]
    private static partial Regex LineChords();

    [GeneratedRegex(@"\s*\{x(?<n>\d+)\}\s*$")]
    private static partial Regex LineRepeat();

    [GeneratedRegex(@"^(?<key>\w+):\s*(?<value>.*)$")]
    private static partial Regex FrontMatterEntry();

    public Song Parse(string id, string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var (meta, bodyStart) = ReadFrontMatter(id, lines);
        var sections = ReadSections(id, lines.AsSpan(bodyStart));

        var arrangement = ParseList(Required(id, meta, "kolejnosc"));
        return new Song(
            Id: id,
            Number: int.Parse(Required(id, meta, "numer"), CultureInfo.InvariantCulture),
            Title: Unquote(Required(id, meta, "tytul")),
            Category: Unquote(Required(id, meta, "kategoria")),
            SourceNumber: ParseNullableInt(meta.GetValueOrDefault("numer_zrodlowy")),
            Key: ParseNullableString(meta.GetValueOrDefault("tonacja")),
            Arrangement: arrangement.Count > 0 ? arrangement : sections.Select(s => s.Code).ToList(),
            Sections: sections);
    }

    private static (Dictionary<string, string> Meta, int BodyStart) ReadFrontMatter(string id, string[] lines)
    {
        if (lines.Length == 0 || lines[0] != "---")
            throw new SongFormatException(id, "brak nagłówka YAML");

        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i] == "---")
                return (meta, i + 1);
            var entry = FrontMatterEntry().Match(lines[i]);
            if (entry.Success)
                meta[entry.Groups["key"].Value] = entry.Groups["value"].Value.Trim();
        }
        throw new SongFormatException(id, "niezamknięty nagłówek YAML");
    }

    private static List<Section> ReadSections(string id, ReadOnlySpan<string> body)
    {
        var sections = new List<Section>();
        SectionBuilder? current = null;
        foreach (var raw in body)
        {
            var line = raw.TrimEnd();
            var header = SectionHeader().Match(line);
            if (header.Success)
            {
                if (current is not null)
                    sections.Add(current.Build());
                current = new SectionBuilder(
                    header.Groups["kod"].Value,
                    header.Groups["nazwa"].Value,
                    header.Groups["n"].Success ? int.Parse(header.Groups["n"].Value, CultureInfo.InvariantCulture) : 1);
            }
            else if (current is not null && line.Length > 0)
            {
                current.Lines.Add(ParseLine(line));
            }
        }
        if (current is not null)
            sections.Add(current.Build());
        if (sections.Count == 0)
            throw new SongFormatException(id, "pieśń nie ma żadnej sekcji");
        return sections;
    }

    internal static SongLine ParseLine(string line)
    {
        if (line.StartsWith('>'))
            return new SongLine(line.TrimStart('>').Trim(), null, 1, false, false, LineKind.Note);

        string? chords = null;
        var chordMatch = LineChords().Match(line);
        if (chordMatch.Success)
        {
            chords = chordMatch.Groups["akordy"].Value.Trim();
            line = line[..chordMatch.Index];
        }

        var repeat = 1;
        var repeatMatch = LineRepeat().Match(line);
        if (repeatMatch.Success)
        {
            repeat = int.Parse(repeatMatch.Groups["n"].Value, CultureInfo.InvariantCulture);
            line = line[..repeatMatch.Index];
        }

        var repeatStart = line.StartsWith("|:", StringComparison.Ordinal);
        if (repeatStart)
            line = line[2..];
        var repeatEnd = line.EndsWith(":|", StringComparison.Ordinal);
        if (repeatEnd)
            line = line[..^2];

        var text = line.Trim();
        var kind = text.Length == 0 && chords is not null ? LineKind.Instrumental : LineKind.Lyric;
        return new SongLine(text, string.IsNullOrEmpty(chords) ? null : chords, repeat, repeatStart, repeatEnd, kind);
    }

    private static string Required(string id, Dictionary<string, string> meta, string key) =>
        meta.TryGetValue(key, out var value) ? value : throw new SongFormatException(id, $"brak klucza {key}");

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string? ParseNullableString(string? value) =>
        value is null or "null" or "" ? null : Unquote(value);

    private static List<string> ParseList(string value) =>
        value.Trim().TrimStart('[').TrimEnd(']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

    private sealed class SectionBuilder(string code, string name, int repeat)
    {
        public List<SongLine> Lines { get; } = [];

        public Section Build() => new(code, name, repeat, Lines);
    }
}
