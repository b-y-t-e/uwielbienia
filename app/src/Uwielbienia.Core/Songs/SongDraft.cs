using System.Text.RegularExpressions;

namespace Uwielbienia.Core.Songs;

/// <summary>Rodzaj części pieśni wybierany w edytorze (kody z tabeli terminologii w CLAUDE.md).</summary>
public sealed record SongPartType(string Kind, string Name)
{
    public static readonly IReadOnlyList<SongPartType> All =
    [
        new("V", "Zwrotka"),
        new("C", "Refren"),
        new("PC", "Przedrefren"),
        new("B", "Mostek"),
        new("I", "Wstęp"),
        new("INT", "Interludium"),
        new("FC", "Refren końcowy"),
        new("T", "Tag"),
        new("E", "Zakończenie"),
        new("O", "Inne"),
    ];

    public static SongPartType ForKind(string kind) =>
        All.FirstOrDefault(t => t.Kind == kind) ?? All[^1];
}

/// <summary>Część w edytorze: rodzaj (pieśń) albo nazwa (tekst) i wersy jako zwykły tekst.</summary>
/// <param name="Text">Jeden wers w jednej linii; akordy na końcu wersu w nawiasie: <c>Panie [G C D]</c>.</param>
/// <param name="OriginalCode">Kod części w edytowanym pliku — żeby zachować kolejność wykonania.</param>
public sealed record DraftPart(string Kind, string Name, string Text, string? OriginalCode = null, int Repeat = 1);

/// <summary>
/// Pieśń lub tekst w trakcie edycji. Zamienia formularz na <see cref="Song"/>: nadaje kody części
/// (<c>V1</c>, <c>C</c>, <c>C2</c>…), ustala kolejność wykonania i tonację.
/// </summary>
public sealed partial record SongDraft(SongKind Kind, string Title, string Category, IReadOnlyList<DraftPart> Parts, Song? Original = null)
{

    public const string LocalSource = "Uwielbienia — własne";

    [GeneratedRegex(@"\s*\[(?<akordy>[^\[\]]*)\]\s*$")]
    private static partial Regex EditorChords();

    public static SongDraft New(SongKind kind) => kind == SongKind.Text
        ? new(kind, "", "", [new DraftPart("O", "", "")])
        : new(kind, "", "Uwielbienie", [new DraftPart("V", "Zwrotka", ""), new DraftPart("C", "Refren", "")]);

    public static SongDraft From(Song song) => new(
        song.Kind,
        song.Title,
        song.Category,
        song.Sections.Select(s => new DraftPart(
            song.IsText ? "O" : s.Kind,
            s.Name,
            string.Join('\n', s.Lines.Select(ToEditorLine)),
            s.Code,
            s.Repeat)).ToList(),
        song);

    /// <summary>Komunikat dla operatora albo <c>null</c>, gdy można zapisać.</summary>
    public string? Validate()
    {
        // Tekst może nie mieć tytułu — przedstawia go wtedy pierwszy wers.
        if (Kind == SongKind.Song && string.IsNullOrWhiteSpace(Title))
            return "Wpisz tytuł.";
        if (!Parts.Any(p => ParseLines(p.Text).Any(l => l.IsSung)))
            return "Wpisz tekst co najmniej jednej części.";
        return null;
    }

    public Song Build(string id, int? number)
    {
        var sections = new List<Section>();
        var codeMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            var lines = ParseLines(part.Text);
            if (lines.Count == 0)
                continue;
            var (code, name) = Kind == SongKind.Text ? TextPartCode(sections.Count + 1) : SongPartCode(part, counters);
            sections.Add(new Section(code, name, part.Repeat, lines));
            if (part.OriginalCode is { } original)
                codeMap[original] = code;
        }

        var arrangement = Arrangement(sections, codeMap);
        return new Song(id, number, Title.Trim(), Category.Trim(), Original?.SourceNumber,
            Kind == SongKind.Text ? null : Original?.Key ?? FirstChord(sections, arrangement),
            arrangement, sections, Kind)
        {
            Source = Original?.Source ?? LocalSource,
        };
    }

    /// <summary>Wersy części w edytorze; tekst nie ma akordów, więc nawiasy zostają zwykłym tekstem.</summary>
    public IReadOnlyList<SongLine> ParseLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0)
            .Select(l => Kind == SongKind.Text ? ParseTextLine(l) : MarkdownSongParser.ParseLine(EditorChords().Replace(l, m => " `" + m.Groups["akordy"].Value.Trim() + "`")))
            .ToList();

    /// <summary>Tytuł albo (tekst bez tytułu) pierwszy wers — np. do identyfikatora pliku.</summary>
    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title)
        ? Title.Trim()
        : Parts.SelectMany(p => ParseLines(p.Text)).FirstOrDefault(l => l.IsSung)?.Text ?? "";

    public static string ToEditorLine(SongLine line)
    {
        if (line.Kind == LineKind.Note)
            return "> " + line.Text;
        var withoutChords = MarkdownSongWriter.WriteLine(line with { Chords = null });
        return line.Chords is { } chords
            ? (withoutChords.Length > 0 ? withoutChords + " " : "") + "[" + chords + "]"
            : withoutChords;
    }

    private static SongLine ParseTextLine(string line)
    {
        var parsed = MarkdownSongParser.ParseLine(line);
        // Tekst nie ma akordów: znaczniki `…` traktujemy jak zwykły tekst wersu.
        return parsed.Chords is null ? parsed : new SongLine(line.Trim(), null, 1, false, false, LineKind.Lyric);
    }

    /// <summary>Części tekstu to po prostu kolejne slajdy: „Część 1”, „Część 2”…</summary>
    private static (string Code, string Name) TextPartCode(int index) => ($"O{index}", $"Część {index}");

    private (string Code, string Name) SongPartCode(DraftPart part, Dictionary<string, int> counters)
    {
        var type = SongPartType.ForKind(part.Kind);
        var n = counters[type.Kind] = counters.GetValueOrDefault(type.Kind) + 1;
        var code = type.Kind == "V" ? $"V{n}" : n == 1 ? type.Kind : $"{type.Kind}{n}";
        var name = type.Kind == "V" || n > 1 ? $"{type.Name} {n}" : type.Name;
        // Własna nazwa części śpiewnika zostaje, dopóki część ma ten sam kod.
        if (part.OriginalCode == code && Original?.FindSection(code) is { } original)
            name = original.Name;
        return (code, name);
    }

    /// <summary>
    /// Tekst: części po kolei. Edytowana pieśń: dotychczasowa kolejność (z nowymi kodami), nowe części na końcu.
    /// Nowa pieśń: refren po każdej zwrotce.
    /// </summary>
    private List<string> Arrangement(List<Section> sections, Dictionary<string, string> codeMap)
    {
        var codes = sections.Select(s => s.Code).ToList();
        if (Kind == SongKind.Text)
            return codes;

        if (Original is not null && codeMap.Count > 0)
        {
            var kept = Original.Arrangement.Where(codeMap.ContainsKey).Select(c => codeMap[c]).ToList();
            var mapped = codeMap.Values.ToHashSet(StringComparer.Ordinal);
            kept.AddRange(codes.Where(c => !mapped.Contains(c)));
            if (kept.Count > 0)
                return kept;
        }

        var chorus = codes.Contains("C") ? "C" : null;
        var result = new List<string>();
        for (var i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            if (code == chorus && result.Count > 0 && result[^1] == chorus)
                continue;
            result.Add(code);
            var isVerse = code.StartsWith('V');
            var nextIsChorus = i + 1 < codes.Count && codes[i + 1] == chorus;
            if (chorus is not null && isVerse && !nextIsChorus)
                result.Add(chorus);
        }
        return result;
    }

    /// <summary>Tonacja = pierwszy akord pieśni (CLAUDE.md).</summary>
    private static string? FirstChord(List<Section> sections, List<string> arrangement)
    {
        foreach (var code in arrangement)
        {
            var chords = sections.FirstOrDefault(s => s.Code == code)?.Lines.FirstOrDefault(l => l.Chords is not null)?.Chords;
            if (chords is null)
                continue;
            var token = chords.Split([' ', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (token is not null)
                return token.Split('/')[0];
        }
        return null;
    }
}
