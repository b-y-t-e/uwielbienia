using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

public class SongParserTests
{
    private const string Sample = """
        ---
        numer: 1
        tytul: "Maryjo, śliczna Pani"
        kategoria: "Pieśni Maryjne"
        numer_zrodlowy: 1069
        tonacja: "G"
        kolejnosc: [V1, C, V2, C]
        zrodlo: "spiewnik.pdf"
        ---

        # Maryjo, śliczna Pani

        ## [V1] Zwrotka 1
        Maryjo, śliczna Pani, `G h`
        > uwaga
        Matko Boga {x2} `C D G`

        ## [C] Refren {x2}
        |: Pierwsza linia `e`
        ostatnia linia :| {x3} `D`
        `G C`

        ## [V2] Zwrotka 2
        Druga zwrotka
        bez akordów
        """;

    private readonly Song _song = new MarkdownSongParser().Parse("001-maryjo", Sample);

    [Fact]
    public void Reads_front_matter()
    {
        Assert.Equal(1, _song.Number);
        Assert.Equal("Maryjo, śliczna Pani", _song.Title);
        Assert.Equal(1069, _song.SourceNumber);
        Assert.Equal("G", _song.Key);
        Assert.Equal(["V1", "C", "V2", "C"], _song.Arrangement);
    }

    [Fact]
    public void Reads_sections_with_repeat()
    {
        Assert.Equal(["V1", "C", "V2"], _song.Sections.Select(s => s.Code));
        Assert.Equal(2, _song.FindSection("C")!.Repeat);
    }

    [Fact]
    public void Reads_line_grammar()
    {
        var chorus = _song.FindSection("C")!.Lines;
        Assert.Equal(new SongLine("Pierwsza linia", "e", 1, true, false, LineKind.Lyric), chorus[0]);
        Assert.Equal(new SongLine("ostatnia linia", "D", 3, false, true, LineKind.Lyric), chorus[1]);
        Assert.Equal(LineKind.Instrumental, chorus[2].Kind);

        var verse = _song.FindSection("V1")!.Lines;
        Assert.Equal(LineKind.Note, verse[1].Kind);
        Assert.Equal(new SongLine("Matko Boga", "C D G", 2, false, false, LineKind.Lyric), verse[2]);
    }

    [Fact]
    public void Inherits_chords_from_first_section_of_same_kind()
    {
        var chords = ChordResolver.EffectiveChords(_song, _song.FindSection("V2")!);
        Assert.Equal(["G h", "C D G"], chords);
    }

    [Fact]
    public void Parses_every_song_in_repository()
    {
        var library = new SongLibrary(new DirectorySongSource(RepositoryPaths.Songs), new MarkdownSongParser());

        Assert.Empty(library.Errors);
        Assert.True(library.Songs.Count >= 138);
        Assert.All(library.Songs, song =>
        {
            Assert.All(song.Arrangement, code => Assert.NotNull(song.FindSection(code)));
            Assert.Contains(song.Sections.SelectMany(s => s.Lines), l => l.IsSung);
        });
    }
}

internal static class RepositoryPaths
{
    public static string Songs { get; } = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Teksty");
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException("Nie znaleziono folderu Teksty/ nad katalogiem testów.");
    }
}
