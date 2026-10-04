using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

public class SearchAndSlidesTests
{
    private static readonly SongLibrary Library =
        new(new DirectorySongSource(RepositoryPaths.Songs), new MarkdownSongParser());

    private readonly SongSearch _search = new(Library);

    [Fact]
    public void Number_query_puts_exact_number_first()
    {
        Assert.Equal(47, _search.Search("47")[0].Number);
    }

    [Fact]
    public void Text_query_ignores_polish_characters_and_punctuation()
    {
        var results = _search.Search("jezus moj pan");
        Assert.Equal(47, results[0].Number);
    }

    [Fact]
    public void Normalize_strips_diacritics()
    {
        Assert.Equal("zolw lodz slonce", SongSearch.Normalize("Żółw, łódź — SŁOŃCE!"));
    }

    [Fact]
    public void Slides_follow_arrangement_and_split_fourLinePartssections()
    {
        var song = Library.Songs.First(s => s.Number == 24);
        var slides = new SectionSlideBuilder(maxLinesPerSlide: 2).Build(song, ["C", "V1"]);

        Assert.Equal("C", slides[0].SectionCode);
        Assert.All(slides, s => Assert.InRange(s.Lines.Count, 1, 2));
        Assert.Contains(slides, s => s.Label.Contains("(1/2)"));
    }

    [Fact]
    public void Short_verse_and_following_chorus_share_one_slide()
    {
        var song = Library.Songs.First(s => s.Number == 24);
        var slides = new SectionSlideBuilder().Build(song, song.Arrangement);

        Assert.Equal(["Refren", "Zwrotka 1 + Refren", "Zwrotka 2 + Refren"], slides.Take(3).Select(s => s.Label));
        Assert.Equal("V1", slides[1].SectionCode);
        var chorus = slides[1].Lines.SkipWhile(l => !l.IsChorus).ToList();
        Assert.True(chorus[0].StartsPart);
        Assert.All(chorus, l => Assert.True(l.IsChorus));
        Assert.DoesNotContain(slides[0].Lines, l => l.IsChorus);
    }

    [Fact]
    public void Verse_and_chorus_stay_apart_when_they_do_not_fit_or_joining_is_off()
    {
        // 6 + 5 wersów — za dużo nawet na zmniejszony slajd
        static Section Part(string code, int lines) => new(code, code, 1,
            [.. Enumerable.Range(1, lines).Select(i => new SongLine($"wers {i}", null, 1, false, false, LineKind.Lyric))]);
        var longParts = new Song("x", 1, "X", "", null, null, ["V1", "C", "V2", "C"], [Part("V1", 6), Part("V2", 6), Part("C", 5)]);
        var twoLineParts = Library.Songs.First(s => s.Number == 24);

        Assert.Equal(4, new SectionSlideBuilder().Build(longParts, longParts.Arrangement).Count);
        Assert.Equal(twoLineParts.Arrangement.Count,
            new SectionSlideBuilder(joinVerseAndChorus: false).Build(twoLineParts, twoLineParts.Arrangement).Count);
    }

    [Fact]
    public void Texts_are_never_joined()
    {
        var song = Library.Songs.First(s => s.Number == 24) with { Kind = SongKind.Text };
        Assert.Equal(song.Arrangement.Count, new SectionSlideBuilder().Build(song, song.Arrangement).Count);
    }
}
