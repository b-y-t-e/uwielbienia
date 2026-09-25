using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

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
    public void Updating_playlist_refreshes_current_item_slides()
    {
        _session.Show(_second, 2);
        var shorter = _second with { Slides = _second.Slides.Take(1).ToList() };
        _session.SetPlaylist([_first, shorter]);
        Assert.Equal((shorter, 0), (_session.State.Item, _session.State.SlideIndex));
    }

    [Fact]
    public void Next_describes_upcoming_slide_or_song()
    {
        _session.Show(_first, 1);
        Assert.Equal("Druga", _session.State.Next);
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
}
