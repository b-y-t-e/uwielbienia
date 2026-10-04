using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Plans;

/// <summary>
/// Plan otwarty w aplikacji: zapisuje każdą zmianę i utrzymuje zgodną z nim kolejkę pieśni w <see cref="LiveSession"/>.
/// </summary>
public sealed class ActivePlan
{
    private readonly IPlanStore _store;
    private readonly ILiveItemFactory _itemFactory;
    private readonly LiveSession _session;

    public ActivePlan(IPlanStore store, ILiveItemFactory itemFactory, LiveSession session, ISongLibrary library)
    {
        _store = store;
        _itemFactory = itemFactory;
        _session = session;
        // Zmieniona pieśń (edycja) trafia od razu do planu i — jeśli jest grana — na ekran.
        library.Changed += (_, _) => Refresh();
    }

    public Plan? Plan { get; private set; }

    public IReadOnlyList<LiveItem> Playlist { get; private set; } = [];

    public event EventHandler? Changed;

    /// <summary>Otwarto inny plan (po <see cref="Changed"/>) — widoki zaczynają od początku planu.</summary>
    public event EventHandler? Opened;

    public void Open(Plan plan)
    {
        Plan = plan;
        Refresh();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Func<Plan, Plan> change)
    {
        if (Plan is null)
            return;
        Plan = change(Plan);
        _store.Save(Plan);
        Refresh();
    }

    public LiveItem? FindLiveItem(Guid planItemId) => Playlist.FirstOrDefault(i => i.PlanItemId == planItemId);

    private void Refresh()
    {
        Playlist = (Plan?.Items ?? []).OfType<SongPlanItem>()
            .Select(_itemFactory.Create)
            .OfType<LiveItem>()
            .ToList();
        _session.SetPlaylist(Playlist);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
