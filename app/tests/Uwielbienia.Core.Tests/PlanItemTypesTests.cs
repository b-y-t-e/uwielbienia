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
    public void Presentation_slides_follow_the_plan_slide_transitions_unless_it_has_its_own()
    {
        var session = new LiveSession();
        var plan = new ActivePlan(new MemoryPlanStore(), new PlanItemTypes([new NoticeType(), new PresentationType()]), session);
        plan.Open(Plan.Create("Test", null).Insert(0, new NoticeItem(Guid.NewGuid(), "a"))
            .Insert(1, new PresentationPlanItem(Guid.NewGuid(), "folder", "Prezentacja"))
            .WithTransitions([SlideTransition.Flame]).WithInnerTransitions([SlideTransition.Ripple]));

        Assert.Equal([SlideTransition.Ripple], plan.Playlist[0].InnerTransitions);
        Assert.Equal([SlideTransition.Ripple], plan.Playlist[1].InnerTransitions);
        Assert.Equal([SlideTransition.Flame], plan.Playlist[1].Transitions); // wejście na prezentację — zawsze z planu

        var own = (PresentationPlanItem)plan.Plan!.Items[1] with { SlideEffects = [SlideTransition.Watercolor] };
        plan.Update(p => p.Replace(own));

        Assert.Equal([SlideTransition.Flame], plan.Playlist[1].Transitions);
        Assert.Equal([SlideTransition.Watercolor], plan.Playlist[1].InnerTransitions);
        var saved = (PresentationPlanItem)System.Text.Json.JsonSerializer.Deserialize<PlanItem>(
            System.Text.Json.JsonSerializer.Serialize<PlanItem>(own, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions)!;
        Assert.Equal([SlideTransition.Watercolor], saved.SlideEffects);
        // plan z dawnym własnym przejściem na pozycję („transitions”) wczytuje się — to pole już nic nie znaczy
        var old = System.Text.Json.JsonSerializer.Deserialize<PlanItem>(
            """{"type":"presentation","id":"8f6c2b1e-0000-0000-0000-000000000001","folder":"f","title":"t","transitions":["flame"]}""", JsonPlanStore.JsonOptions);
        Assert.Null(old!.InnerTransitions);
    }

    [Fact]
    public void Song_slides_follow_the_plan_slide_transitions_unless_it_has_its_own()
    {
        var session = new LiveSession();
        var library = new Songs.SongLibrary(new Songs.DirectorySongSource(RepositoryPaths.Songs), new Songs.MarkdownSongParser());
        var plan = new ActivePlan(new MemoryPlanStore(), new PlanItemTypes([new SongPlanItemType(library, new SectionSlideBuilder())]), session);
        var songId = library.Songs[0].Id;
        var own = SongPlanItem.For(songId) with { SlideEffects = [] };

        plan.Open(Plan.Create("Test", null).Insert(0, own).Insert(1, SongPlanItem.For(songId)).WithInnerTransitions([SlideTransition.Flame]));

        Assert.Empty(plan.Playlist[0].InnerTransitions!);
        Assert.Equal([SlideTransition.Flame], plan.Playlist[1].InnerTransitions);
        var saved = System.Text.Json.JsonSerializer.Deserialize<PlanItem>(
            System.Text.Json.JsonSerializer.Serialize<PlanItem>(own, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions);
        Assert.Empty(((SongPlanItem)saved!).SlideEffects!);
    }

    private sealed class PresentationType : IPlanItemType
    {
        public bool Handles(PlanItem item) => item is PresentationPlanItem;

        public PlanItemInfo? Describe(PlanItem item) => new(((PresentationPlanItem)item).Title, null, PlanItemKind.Presentation);

        public LiveItem? Present(PlanItem item) =>
            new(item.Id, "prezentacja", null, "Prezentacja", [new Slide("1", "1", []), new Slide("2", "2", [])]);

        public event EventHandler? Changed { add { } remove { } }
    }

    [Fact]
    public void Plan_transitions_apply_between_visible_plan_slides()
    {
        var effects = new[] { SlideTransition.Flame };
        LiveItem Item(Guid? planItemId) => new(planItemId, "x", null, "T", [new Slide("V1", "1", []), new Slide("V2", "2", [])], Transitions: effects, InnerTransitions: [SlideTransition.Ripple]);
        var first = Item(Guid.NewGuid());
        var second = Item(Guid.NewGuid());
        LiveState State(LiveItem item, int slide = 0, bool blank = false) => new(1, item, slide, blank, null);

        Assert.Equal(effects, State(second).TransitionsFrom(State(first)));
        Assert.Equal([SlideTransition.Ripple], State(first, 1).TransitionsFrom(State(first)));
        Assert.Empty(State(first).TransitionsFrom(State(first)));
        Assert.Empty(State(first with { InnerTransitions = [] }, 1).TransitionsFrom(State(first)));
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
    public void No_effect_is_a_choice_of_its_own_and_survives_saving()
    {
        var plan = Plan.Create("Test", null).WithTransitions(SlideTransitions.Cut).WithInnerTransitions(SlideTransitions.Cut);

        Assert.Equal("brak efektu", SlideTransitions.Describe(plan.ChosenTransitions));
        Assert.True(SlideTransitions.IsCut(plan.ChosenInnerTransitions));
        Assert.DoesNotContain(SlideTransition.None, SlideTransitions.All);
        Assert.Equal(SlideTransition.None, SlideTransitions.Pick(SlideTransitions.Cut, new Random(1)));
        var saved = System.Text.Json.JsonSerializer.Deserialize<Plan>(
            System.Text.Json.JsonSerializer.Serialize(plan, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions)!;
        Assert.Equal([SlideTransition.None], saved.Transitions);
        Assert.Equal([SlideTransition.None], saved.InnerTransitions);
    }

    [Fact]
    public void Slide_transitions_follow_the_plan_until_chosen()
    {
        var plan = Plan.Create("Test", null).WithTransitions([SlideTransition.StainedGlass]);

        Assert.Equal([SlideTransition.StainedGlass], plan.ChosenInnerTransitions);
        var own = plan.WithInnerTransitions([]);
        Assert.Empty(own.ChosenInnerTransitions);
        Assert.Null(own.WithInnerTransitions(null).InnerTransitions);
        var saved = System.Text.Json.JsonSerializer.Deserialize<Plan>(
            System.Text.Json.JsonSerializer.Serialize(own, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions);
        Assert.Empty(saved!.InnerTransitions!);
        Assert.Empty(own.CloneAs("Kopia", null, PlanKind.Template).InnerTransitions!);
    }

    [Fact]
    public void New_effects_reach_the_item_on_screen_without_changing_the_slide()
    {
        var shown = new LiveItem(Guid.NewGuid(), "x", null, "T", [new Slide("V1", "1", []), new Slide("C", "2", [])],
            Transitions: [SlideTransition.Flame], InnerTransitions: [SlideTransition.Flame]);
        var session = new LiveSession();
        session.SetPlaylist([shown]);
        session.Show(shown, 1);

        session.SetPlaylist([shown with { Transitions = [SlideTransition.Ripple], InnerTransitions = [SlideTransition.LightRay] }]);

        Assert.Equal(1, session.State.SlideIndex);
        Assert.Equal([SlideTransition.Ripple], session.State.Item!.Transitions);
        Assert.Equal([SlideTransition.LightRay], session.State.Item.InnerTransitions);
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
