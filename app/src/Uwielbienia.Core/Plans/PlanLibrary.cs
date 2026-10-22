using System.Globalization;

namespace Uwielbienia.Core.Plans;

/// <summary>
/// Wszystkie plany (wydarzenia i szablony): lista, tworzenie, zmiana nazwy i daty, otwieranie, kopie, usuwanie.
/// Jedno miejsce dla okna „Plany” i panelu w przeglądarce. Otwarty plan zmienia się przez <see cref="ActivePlan"/>.
/// </summary>
public sealed class PlanLibrary(IPlanStore store, ActivePlan active)
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public const string DefaultEventName = "Uwielbienie";

    /// <summary>Zmieniła się lista planów (nowy, zmieniony, usunięty plan).</summary>
    public event EventHandler? Changed;

    /// <summary>Błąd ostatniego zapisu planu innego niż otwarty (<c>null</c> = zapisany) — zmiana i tak zostaje w pamięci magazynu.</summary>
    public Exception? SaveError { get; private set; }

    /// <summary>Wydarzenia od najnowszej daty, szablony alfabetycznie.</summary>
    public IReadOnlyList<Plan> List(PlanKind kind)
    {
        var plans = store.LoadAll().Where(p => p.Kind == kind)
            // otwarty plan — zawsze w wersji z pamięci (zapis mógł się nie udać)
            .Select(p => p.Id == active.Plan?.Id ? active.Plan : p);
        return (kind == PlanKind.Event
                ? plans.OrderByDescending(p => p.Date ?? DateOnly.MinValue).ThenByDescending(p => p.UpdatedAt)
                : plans.OrderBy(p => p.Name, StringComparer.Create(Polish, ignoreCase: false)))
            .ToList();
    }

    public Plan? Find(Guid planId) =>
        planId == active.Plan?.Id ? active.Plan : store.LoadAll().FirstOrDefault(p => p.Id == planId);

    /// <summary>Nazwa zawsze wymagana; data tylko dla wydarzenia i tylko w osobnym polu, nie w nazwie.</summary>
    public static bool IsValid(string? name, DateOnly? date, PlanKind kind) =>
        !string.IsNullOrWhiteSpace(name) && (kind == PlanKind.Template || date is not null);

    /// <summary>Nowy plan, od razu otwarty.</summary>
    public Plan Create(string name, DateOnly? date, PlanKind kind)
    {
        var plan = Plan.Create(name.Trim(), kind == PlanKind.Template ? null : date, kind);
        Save(plan);
        Changed?.Invoke(this, EventArgs.Empty);
        active.Open(plan);
        return plan;
    }

    /// <summary>Nazwa i data planu (także otwartego).</summary>
    public Plan? Rename(Guid planId, string name, DateOnly? date)
    {
        if (Find(planId) is not { } plan || !IsValid(name, date, plan.Kind))
            return null;
        Plan Renamed(Plan p) => p with
        {
            Name = name.Trim(), Date = p.Kind == PlanKind.Template ? null : date, UpdatedAt = DateTimeOffset.Now,
        };
        if (planId == active.Plan?.Id)
            active.Update(Renamed);
        else
            Save(Renamed(plan));
        Changed?.Invoke(this, EventArgs.Empty);
        return Find(planId);
    }

    /// <summary>Otwiera plan; ten sam plan — bez zmian (widoki nie wracają na początek).</summary>
    public Plan? Open(Guid planId)
    {
        if (planId == active.Plan?.Id)
            return active.Plan;
        if (Find(planId) is not { } plan)
            return null;
        active.Open(plan);
        return plan;
    }

    /// <summary>Przy starcie: ostatnio otwarty plan albo nowe „Uwielbienie” z dzisiejszą datą.</summary>
    public Plan OpenInitial(Guid? lastPlanId) =>
        (lastPlanId is { } id ? Open(id) : null) ?? Create(DefaultEventName, Today, PlanKind.Event);

    /// <summary>Nowe wydarzenie na dziś z tymi samymi pozycjami — z szablonu albo z wcześniejszego wydarzenia; otwarte.</summary>
    public Plan? CopyToToday(Guid planId)
    {
        if (Find(planId) is not { } plan)
            return null;
        var copy = plan.CloneAs(NameWithoutDate(plan), Today, PlanKind.Event);
        Save(copy);
        Changed?.Invoke(this, EventArgs.Empty);
        active.Open(copy);
        return copy;
    }

    /// <summary>Szablon z tymi samymi pozycjami (otwarty plan się nie zmienia).</summary>
    public Plan? SaveAsTemplate(Guid planId)
    {
        if (Find(planId) is not { } plan)
            return null;
        var template = plan.CloneAs(plan.Name, null, PlanKind.Template);
        Save(template);
        Changed?.Invoke(this, EventArgs.Empty);
        return template;
    }

    /// <summary>Usuwa plan; otwartego nie można usunąć.</summary>
    public bool Delete(Guid planId)
    {
        if (planId == active.Plan?.Id || Find(planId) is null)
            return false;
        store.Delete(planId);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Plik chwilowo zajęty — plan zostaje w <see cref="PendingPlanStore"/>, następna zmiana zapisze całość.</summary>
    private void Save(Plan plan) => SaveError = store.TrySave(plan);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Starsze plany miały datę w nazwie — kopia na inny dzień nie powinna jej powielać.</summary>
    private static string NameWithoutDate(Plan plan)
    {
        if (plan.Date is not { } date)
            return plan.Name;
        var name = plan.Name.Replace(date.ToString("d MMMM yyyy", Polish), "", StringComparison.CurrentCultureIgnoreCase).Trim(' ', '—', '-');
        return name.Length > 0 ? name : DefaultEventName;
    }
}
