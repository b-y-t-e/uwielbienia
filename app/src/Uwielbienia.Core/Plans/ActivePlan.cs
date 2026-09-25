using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Plans;

/// <summary>
/// Plan otwarty w aplikacji: zapisuje każdą zmianę i utrzymuje zgodną z nim kolejkę pieśni w <see cref="LiveSession"/>.
/// </summary>
public sealed class ActivePlan(IPlanStore store, ILiveItemFactory itemFactory, LiveSession session)
{
    public Plan? Plan { get; private set; }

    public IReadOnlyList<LiveItem> Playlist { get; private set; } = [];

    public event EventHandler? Changed;

    public void Open(Plan plan)
    {
        Plan = plan;
        Refresh();
    }

    public void Update(Func<Plan, Plan> change)
    {
        if (Plan is null)
            return;
        Plan = change(Plan);
        store.Save(Plan);
        Refresh();
    }

    public LiveItem? FindLiveItem(Guid planItemId) => Playlist.FirstOrDefault(i => i.PlanItemId == planItemId);

    private void Refresh()
    {
        Playlist = (Plan?.Items ?? []).OfType<SongPlanItem>()
            .Select(itemFactory.Create)
            .OfType<LiveItem>()
            .ToList();
        session.SetPlaylist(Playlist);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
