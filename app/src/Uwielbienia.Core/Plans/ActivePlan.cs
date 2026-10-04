using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Plans;

/// <summary>
/// Plan otwarty w aplikacji: zapisuje każdą zmianę i utrzymuje zgodną z nim kolejkę pozycji w <see cref="LiveSession"/>.
/// Pozycje każdego rodzaju zamienia na slajdy <see cref="IPlanItemTypes"/>.
/// </summary>
public sealed class ActivePlan
{
    private readonly IPlanStore _store;
    private readonly IPlanItemTypes _itemTypes;
    private readonly LiveSession _session;

    public ActivePlan(IPlanStore store, IPlanItemTypes itemTypes, LiveSession session, ISongLibrary library)
    {
        _store = store;
        _itemTypes = itemTypes;
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
        Playlist = (Plan?.Items ?? [])
            .Select(_itemTypes.Present)
            .OfType<LiveItem>()
            .ToList();
        _session.SetPlaylist(Playlist);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
