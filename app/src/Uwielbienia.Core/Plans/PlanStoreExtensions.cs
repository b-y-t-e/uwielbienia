namespace Uwielbienia.Core.Plans;

public static class PlanStoreExtensions
{
    /// <summary>Zapisuje plan; błąd dostępu do pliku zwraca (<c>null</c> = zapisany) — plan zostaje w <see cref="PendingPlanStore"/>.</summary>
    public static Exception? TrySave(this IPlanStore store, Plan plan)
    {
        try
        {
            store.Save(plan);
            return null;
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            return ex;
        }
    }
}
