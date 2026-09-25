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
    public void Slides_follow_arrangement_and_split_long_sections()
    {
        var song = Library.Songs.First(s => s.Number == 24);
        var slides = new SectionSlideBuilder(maxLinesPerSlide: 2).Build(song, ["C", "V1"]);

        Assert.Equal("C", slides[0].SectionCode);
        Assert.All(slides, s => Assert.InRange(s.Lines.Count, 1, 2));
        Assert.Contains(slides, s => s.Label.Contains("(1/2)"));
    }
}
