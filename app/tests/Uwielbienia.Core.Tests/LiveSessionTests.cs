using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

public class LiveSessionTests
{
    private static LiveItem Item(string title, int slides) =>
        new(Guid.NewGuid(), title, 1, title,
            Enumerable.Range(1, slides).Select(i => new Slide("V" + i, "Zwrotka " + i, [new SlideLine("t", null, 1, false, false)])).ToList());

    private readonly LiveItem _first = Item("Pierwsza", 2);
    private readonly LiveItem _second = Item("Druga", 3);
    private readonly LiveSession _session = new();

    public LiveSessionTests() => _session.SetPlaylist([_first, _second]);

    [Fact]
    public void Next_moves_through_slides_and_into_next_song()
    {
        _session.Next();
        Assert.Equal((_first, 0), (_session.State.Item, _session.State.SlideIndex));
        _session.Next();
        _session.Next();
        Assert.Equal((_second, 0), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Previous_from_first_slide_goes_to_last_slide_of_previous_song()
    {
        _session.Show(_second);
        _session.Previous();
        Assert.Equal((_first, 1), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Next_at_end_of_plan_stays()
    {
        _session.Show(_second, 2);
        _session.Next();
        Assert.Equal((_second, 2), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Blank_hides_slide_and_survives_navigation()
    {
        _session.Show(_first);
        _session.ToggleBlank();
        _session.Next();
        Assert.True(_session.State.IsBlank);
        Assert.Null(_session.State.VisibleSlide);
        Assert.NotNull(_session.State.Slide);
    }

    [Fact]
    public void Skipping_other_part_of_current_song_keeps_the_same_slide_on_screen()
    {
        _session.Show(_second, 2);
        var withoutFirst = _second with { Slides = _second.Slides.Skip(1).ToList() };
        _session.SetPlaylist([_first, withoutFirst]);
        Assert.Equal((withoutFirst, 1), (_session.State.Item, _session.State.SlideIndex));
        Assert.Equal("V3", _session.State.Slide!.SectionCode);
    }

    [Fact]
    public void Skipping_the_part_on_screen_moves_to_the_next_kept_part()
    {
        _session.Show(_second, 1);
        var withoutMiddle = _second with { Slides = [_second.Slides[0], _second.Slides[2]] };
        _session.SetPlaylist([_first, withoutMiddle]);
        Assert.Equal("V3", _session.State.Slide!.SectionCode);
    }

    [Fact]
    public void Skipping_the_last_part_on_screen_moves_back_to_the_previous_kept_part()
    {
        _session.Show(_second, 2);
        var withoutLast = _second with { Slides = _second.Slides.Take(2).ToList() };
        _session.SetPlaylist([_first, withoutLast]);
        Assert.Equal("V2", _session.State.Slide!.SectionCode);
    }

    [Fact]
    public void Restoring_a_part_keeps_the_same_slide_on_screen()
    {
        var withoutFirst = _second with { Slides = _second.Slides.Skip(1).ToList() };
        _session.SetPlaylist([_first, withoutFirst]);
        _session.Show(withoutFirst, 0);
        _session.SetPlaylist([_first, _second]);
        Assert.Equal((_second, 1), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Plan_change_without_new_parts_keeps_the_same_snapshot()
    {
        _session.Show(_second, 1);
        var rebuilt = _second with { Slides = _second.Slides.ToList() };
        _session.SetPlaylist([rebuilt, _first]);
        Assert.Same(_second, _session.State.Item);
        Assert.Equal(1, _session.State.SlideIndex);
    }

    [Fact]
    public void Skipping_all_parts_of_current_song_clears_text_and_next_goes_to_following_song()
    {
        _session.Show(_first, 1);
        var nothing = _first with { Slides = [] };
        _session.SetPlaylist([nothing, _second]);
        Assert.Null(_session.State.Slide);
        _session.Next();
        Assert.Equal((_second, 0), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Next_skips_songs_with_all_parts_skipped()
    {
        var third = Item("Trzecia", 1);
        _session.SetPlaylist([_first, _second with { Slides = [] }, third]);
        _session.Show(_first, 1);
        _session.Next();
        Assert.Equal(third, _session.State.Item);
    }

    [Fact]
    public void Reordering_parts_keeps_the_same_part_on_screen()
    {
        _session.Show(_second, 0);
        var reordered = _second with { Slides = [_second.Slides[2], _second.Slides[0], _second.Slides[1]] };
        _session.SetPlaylist([_first, reordered]);
        Assert.Equal("V1", _session.State.Slide!.SectionCode);
        Assert.Equal(1, _session.State.SlideIndex);
    }

    [Fact]
    public void Song_shown_outside_the_plan_is_not_replaced()
    {
        var outside = _second with { PlanItemId = null };
        _session.Show(outside, 2);
        _session.SetPlaylist([_first]);
        Assert.Equal((outside, 2), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Next_describes_upcoming_slide_or_song()
    {
        _session.Show(_first, 1);
        Assert.Equal("Druga", _session.State.Next);
    }

    [Fact]
    public void Skipping_the_chorus_keeps_its_verse_on_screen()
    {
        var line = new SlideLine("t", null, 1, false, false);
        var id = Guid.NewGuid();
        LiveItem joined = new(id, "s", 1, "S",
            [new("C", "Refren", [line]), new("V1", "Zwrotka 1 + Refren", [line, line]), new("V2", "Zwrotka 2 + Refren", [line, line])]);
        var session = new LiveSession();
        session.SetPlaylist([joined]);
        session.Show(joined, 2);

        session.SetPlaylist([joined with { Slides = [new("V1", "Zwrotka 1", [line]), new("V2", "Zwrotka 2", [line])] }]);

        Assert.Equal("Zwrotka 2", session.State.Slide!.Label);
    }

    [Fact]
    public void Plan_clone_gets_new_ids()
    {
        var plan = Plan.Create("Próba z dziećmi", null, PlanKind.Template).Insert(0, SongPlanItem.For("001-a"));
        var copy = plan.CloneAs("Uwielbienie", new DateOnly(2026, 9, 25), PlanKind.Event);
        Assert.NotEqual(plan.Id, copy.Id);
        Assert.NotEqual(plan.Items[0].Id, copy.Items[0].Id);
        Assert.Equal("001-a", ((SongPlanItem)copy.Items[0]).SongId);
    }

    [Fact]
    public void Note_of_shown_item_updates_without_new_slides()
    {
        var session = new LiveSession();
        var id = Guid.NewGuid();
        var preparing = new LiveItem(id, "p", null, "Prezentacja", [], ShowTitle: false, Note: "Przygotowywanie slajdów…");
        session.SetPlaylist([preparing]);
        session.Show(preparing);

        session.SetPlaylist([preparing with { Note = "Brak PowerPointa" }]);

        Assert.Equal("Brak PowerPointa", session.State.Item!.Note);
    }

    [Theory]
    [InlineData("Zaufałem Panu i już", "Zaufałem Panu i już|Niczego nie muszę się lękać", 1)]
    [InlineData("Zaufałem Panu", "Zaufałem Panu i już,|Niczego", 1)]
    [InlineData("Jezus mój Pan", "JEZUS, mój Pan!", 1)]
    [InlineData("Jezus pokonał śmierć", "Jezus|pokonał śmierć, zmartwychwstał|Alleluja", 2)]
    [InlineData("Jezus", "Jezusie mój", 0)]
    [InlineData("Taki jesteś Ty", "Jesteś tu, jesteś pośród nas", 0)]
    [InlineData("", "Ojcze nasz", 0)]
    public void Lines_repeating_the_title_are_counted(string title, string firstLines, int count)
    {
        // wersy pierwszego slajdu rozdzielone „|”
        Slide[] slides = [new("V1", "Zwrotka 1", [.. firstLines.Split('|').Select(l => new SlideLine(l, null, 1, false, false))])];
        Assert.Equal(count, SongPlanItemType.TitleLineCount(title, slides));
    }

    [Fact]
    public void Song_starting_with_its_title_shows_no_title_but_colors_the_first_line()
    {
        var library = new SongLibrary(new DirectorySongSource(RepositoryPaths.Songs), new MarkdownSongParser());
        var song = library.Songs.First(s => s.Number == 47);
        var live = new SongPlanItemType(library, new SectionSlideBuilder()).Present(song);

        Assert.False(live.ShowTitle);
        Assert.True(live.Slides[0].Lines[0].IsTitle);
        Assert.DoesNotContain(live.Slides.Skip(1).SelectMany(sl => sl.Lines), l => l.IsTitle);
    }
}
