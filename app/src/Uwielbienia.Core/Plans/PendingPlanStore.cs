namespace Uwielbienia.Core.Plans;

/// <summary>
/// Plany, których nie dało się zapisać (plik zajęty przez antywirusa, synchronizację), zostają tu w pamięci:
/// każdy odczyt widzi ich najnowszą wersję, a następny udany zapis — także innej czynności na tym planie — zapisuje całość.
/// Błąd zapisu idzie dalej, żeby wywołujący mógł o nim powiedzieć.
/// </summary>
public sealed class PendingPlanStore(IPlanStore inner) : IPlanStore
{
    // panel czyta listę planów z wątku połączenia, a okno zmienia plany na wątku interfejsu
    private readonly Dictionary<Guid, Plan> _pending = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<Plan> LoadAll()
    {
        var saved = inner.LoadAll();
        lock (_lock)
            return saved
            .Select(plan => _pending.GetValueOrDefault(plan.Id) ?? plan)
            .Concat(_pending.Values.Where(plan => saved.All(s => s.Id != plan.Id)))
            .ToList();
    }

    public void Save(Plan plan)
    {
        try
        {
            inner.Save(plan);
            lock (_lock)
                _pending.Remove(plan.Id);
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            lock (_lock)
                _pending[plan.Id] = plan;
            throw;
        }
    }

    public void Delete(Guid planId)
    {
        inner.Delete(planId);
        lock (_lock)
            _pending.Remove(planId);
    }
}
