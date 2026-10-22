using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Tests;

/// <summary>Plik planu chwilowo zajęty: plan zostaje w pamięci, a następny udany zapis zapisuje całość.</summary>
public sealed class PendingPlanStoreTests
{
    private readonly LockableStore _file = new();
    private readonly PendingPlanStore _store;

    public PendingPlanStoreTests() => _store = new PendingPlanStore(_file);

    private static Plan NewPlan(string name) => Plan.Create(name, null, PlanKind.Template);

    [Fact]
    public void Unsaved_plan_is_visible_and_saved_by_next_change()
    {
        var plan = NewPlan("A");
        _file.Locked = true;
        Assert.Throws<IOException>(() => _store.Save(plan));
        Assert.Contains(plan, _store.LoadAll());

        _file.Locked = false;
        var renamed = plan with { Name = "B" };
        _store.Save(renamed);
        Assert.Equal([renamed], _store.LoadAll());
        Assert.Equal([renamed], _file.LoadAll());
    }

    [Fact]
    public void Unsaved_version_replaces_saved_one()
    {
        var plan = NewPlan("A");
        _store.Save(plan);
        _file.Locked = true;
        var renamed = plan with { Name = "B" };
        Assert.Throws<IOException>(() => _store.Save(renamed));
        Assert.Equal([renamed], _store.LoadAll());
    }

    [Fact]
    public void Library_rename_with_locked_file_works_in_app()
    {
        var library = new PlanLibrary(_store, new ActivePlan(_store, new PlanItemTypes([]), new LiveSession()));
        var plan = NewPlan("A");
        _store.Save(plan);
        _file.Locked = true;
        var changed = false;
        library.Changed += (_, _) => changed = true;

        library.Rename(plan.Id, "B", null);

        Assert.True(changed);
        Assert.Equal("B", library.List(PlanKind.Template).Single().Name);
        Assert.IsType<IOException>(library.SaveError);
    }

    private sealed class LockableStore : IPlanStore
    {
        private readonly Dictionary<Guid, Plan> _plans = [];

        public bool Locked { get; set; }

        public IReadOnlyList<Plan> LoadAll() => _plans.Values.ToList();

        public void Save(Plan plan)
        {
            if (Locked)
                throw new IOException("zajęty");
            _plans[plan.Id] = plan;
        }

        public void Delete(Guid planId) => _plans.Remove(planId);
    }
}
