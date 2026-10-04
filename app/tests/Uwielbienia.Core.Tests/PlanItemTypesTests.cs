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
    public void Unknown_item_type_is_skipped()
    {
        var types = new PlanItemTypes([]);
        var notice = new NoticeItem(Guid.NewGuid(), "x");
        Assert.Null(types.Describe(notice));
        Assert.Null(types.Present(notice));
    }
}
