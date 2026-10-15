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
            s.Repeat)).DefaultIfEmpty(new DraftPart("O", "", "")).ToList(),
        song);

    /// <summary>Komunikat dla operatora albo <c>null</c>, gdy można zapisać.</summary>
    public string? Validate()
    {
        // Tekst może nie mieć tytułu — przedstawia go wtedy pierwszy wers.
        if (Kind == SongKind.Song && string.IsNullOrWhiteSpace(Title))
            return "Wpisz tytuł.";
        // Tekst z samym tytułem (np. „Uwielbienie”) też jest poprawny — na ekranie sam tytuł.
        if (!Parts.Any(p => ParseLines(p.Text).Any(l => l.IsSung)) && (Kind == SongKind.Song || string.IsNullOrWhiteSpace(Title)))
            return Kind == SongKind.Text ? "Wpisz tytuł albo treść." : "Wpisz tekst co najmniej jednej części.";
        return null;
    }

    public Song Build(string id, int? number)
    {
        var sections = new List<Section>();
        var codeMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var filled = Parts.Where(p => ParseLines(p.Text).Count > 0).ToList();
        // Kod części zostaje taki sam po edycji (także po przestawieniu i usunięciu innych części), żeby plany,
        // które go zapamiętały (pominięcia, kolejność), dalej wskazywały tę samą treść. Nowy kod — tylko
        // nowa część albo zmieniony rodzaj.
        var used = filled.Select(KeptCode).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var part in filled)
        {
            var code = KeptCode(part) ?? NewCode(part, used);
            var name = Kind == SongKind.Text ? $"Część {sections.Count + 1}" : SongPartName(part, code);
            sections.Add(new Section(code, name, part.Repeat, ParseLines(part.Text)));
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

    private string PartKind(DraftPart part) => Kind == SongKind.Text ? "O" : SongPartType.ForKind(part.Kind).Kind;

    /// <summary>Dotychczasowy kod części, jeśli nadal pasuje do jej rodzaju (pierwsza część z tym kodem).</summary>
    private string? KeptCode(DraftPart part) =>
        part.OriginalCode is { } code && KindOf(code) == PartKind(part)
                                     && Parts.First(p => p.OriginalCode == code && ParseLines(p.Text).Count > 0) == part
            ? code
            : null;

    private static string KindOf(string code) => code.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

    /// <summary>Pierwszy wolny kod rodzaju: <c>V1</c>, <c>V2</c>…; <c>C</c>, <c>C2</c>…; tekst <c>O1</c>, <c>O2</c>…</summary>
    private string NewCode(DraftPart part, HashSet<string> used)
    {
        var kind = PartKind(part);
        for (var n = 1; ; n++)
        {
            var code = kind is "V" or "O" ? $"{kind}{n}" : n == 1 ? kind : $"{kind}{n}";
            if (used.Add(code))
                return code;
        }
    }

    private string SongPartName(DraftPart part, string code)
    {
        // Własna nazwa części śpiewnika zostaje, dopóki część ma ten sam kod.
        if (part.OriginalCode == code && Original?.FindSection(code) is { } original)
            return original.Name;
        var type = SongPartType.ForKind(KindOf(code));
        var number = code[type.Kind.Length..];
        return type.Kind == "V" || number.Length > 0 ? $"{type.Name} {(number.Length > 0 ? number : "1")}" : type.Name;
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
