using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Tests;

/// <summary>Nowy rodzaj pozycji planu działa bez zmian w planie i na ekranie — wystarczy <see cref="IPlanItemType"/>.</summary>
public class PlanItemTypesTests
{
    private sealed record NoticeItem(Guid Id, string Message) : PlanItem(Id)
    {
        public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };
    }

    private sealed record NoticeSlide(string Message) : Slide("N", "Ogłoszenie", []);

    private sealed class NoticeType : IPlanItemType
    {
        public bool Handles(PlanItem item) => item is NoticeItem;

        public PlanItemInfo? Describe(PlanItem item) => new(((NoticeItem)item).Message, null, PlanItemKind.Text);

        public LiveItem? Present(PlanItem item) =>
            new(item.Id, "ogloszenie", null, "Ogłoszenie", [new NoticeSlide(((NoticeItem)item).Message)]);
        public event EventHandler? Changed { add { } remove { } }
    }

    private sealed class MemoryPlanStore : IPlanStore
    {
        public IReadOnlyList<Plan> LoadAll() => [];

        public void Save(Plan plan) { }

        public void Delete(Guid planId) { }
    }

    [Fact]
    public void New_item_type_reaches_the_screen_through_the_plan()
    {
        var session = new LiveSession();
        var library = new Songs.SongLibrary(new Songs.DirectorySongSource(RepositoryPaths.Songs), new Songs.MarkdownSongParser());
        var types = new PlanItemTypes([new SongPlanItemType(library, new SectionSlideBuilder()), new NoticeType()]);
        var plan = new ActivePlan(new MemoryPlanStore(), types, session);
        var notice = new NoticeItem(Guid.NewGuid(), "Kawa po Mszy");

        plan.Open(Plan.Create("Test", null).Insert(0, notice));
        session.Next();

        Assert.Equal("Kawa po Mszy", types.Describe(notice)!.Title);
        Assert.IsType<NoticeSlide>(session.State.Slide);
    }

    [Fact]
    public void Every_item_of_the_plan_carries_the_plan_transitions_and_follows_their_change()
    {
        var session = new LiveSession();
        var plan = new ActivePlan(new MemoryPlanStore(), new PlanItemTypes([new NoticeType()]), session);
        plan.Open(Plan.Create("Test", null).Insert(0, new NoticeItem(Guid.NewGuid(), "a")).Insert(1, new NoticeItem(Guid.NewGuid(), "b")));

        Assert.All(plan.Playlist, item => Assert.Equal(Plan.DefaultTransitions, item.Transitions));

        plan.Update(p => p with { Transitions = [SlideTransition.Flame, SlideTransition.Slide] });

        Assert.All(plan.Playlist, item => Assert.Equal([SlideTransition.Flame, SlideTransition.Slide], item.Transitions));
    }

    [Fact]
    public void Song_with_own_transitions_uses_them_and_one_without_follows_the_plan()
    {
        var session = new LiveSession();
        var library = new Songs.SongLibrary(new Songs.DirectorySongSource(RepositoryPaths.Songs), new Songs.MarkdownSongParser());
        var plan = new ActivePlan(new MemoryPlanStore(), new PlanItemTypes([new SongPlanItemType(library, new SectionSlideBuilder())]), session);
        var songId = library.Songs[0].Id;
        var own = SongPlanItem.For(songId) with { Transitions = [SlideTransition.Flame] };
        var plain = SongPlanItem.For(songId);

        plan.Open(Plan.Create("Test", null).Insert(0, own).Insert(1, plain).WithTransitions([SlideTransition.Ripple]));

        Assert.Equal([SlideTransition.Flame], plan.Playlist[0].Transitions);
        Assert.Equal([SlideTransition.Ripple], plan.Playlist[1].Transitions);
        var saved = System.Text.Json.JsonSerializer.Deserialize<PlanItem>(
            System.Text.Json.JsonSerializer.Serialize<PlanItem>(own, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions);
        Assert.Equal([SlideTransition.Flame], ((SongPlanItem)saved!).Transitions);
    }

    [Fact]
    public void Plan_transitions_apply_only_between_two_visible_plan_items()
    {
        var effects = new[] { SlideTransition.Flame };
        LiveItem Item(Guid? planItemId) => new(planItemId, "x", null, "T", [new Slide("V1", "1", []), new Slide("V2", "2", [])], Transitions: effects);
        var first = Item(Guid.NewGuid());
        var second = Item(Guid.NewGuid());
        LiveState State(LiveItem item, int slide = 0, bool blank = false) => new(1, item, slide, blank, null);

        Assert.Equal(effects, State(second).TransitionsFrom(State(first)));
        Assert.Empty(State(first, 1).TransitionsFrom(State(first)));
        Assert.Empty(State(second, blank: true).TransitionsFrom(State(first)));
        Assert.Empty(State(second).TransitionsFrom(State(first, blank: true)));
        Assert.Empty(State(second).TransitionsFrom(LiveState.Empty));
        Assert.Empty(State(Item(null) with { Transitions = null }).TransitionsFrom(State(first)));
        Assert.Empty(State(second with { Transitions = [] }).TransitionsFrom(State(first)));
        Assert.Empty(State(second).TransitionsFrom(State(Item(null) with { Transitions = null })));
    }

    [Fact]
    public void Choosing_the_default_transitions_stores_none_and_describes_the_choice()
    {
        var plan = Plan.Create("Test", null);

        Assert.Null(plan.WithTransitions([SlideTransition.Watercolor]).Transitions);
        Assert.Equal([SlideTransition.Flame], plan.WithTransitions([SlideTransition.Flame]).Transitions);
        Assert.Equal("Akwarela", SlideTransitions.Describe(plan.ChosenTransitions));
        Assert.Equal("przenikanie", SlideTransitions.Describe([]));
        Assert.Equal("losowe (3)", SlideTransitions.Describe([SlideTransition.Flame, SlideTransition.Slide, SlideTransition.Ripple]));
    }

    [Fact]
    public void Unknown_item_type_is_skipped()
    {
        var types = new PlanItemTypes([]);
        var notice = new NoticeItem(Guid.NewGuid(), "x");
        Assert.Null(types.Describe(notice));
        Assert.Null(types.Present(notice));
    }
}
