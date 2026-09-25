using System.Text.Json.Serialization;

namespace Uwielbienia.Core.Plans;

public enum PlanKind
{
    /// <summary>Konkretne wydarzenie, np. „Uwielbienie 25 września 2026”.</summary>
    Event,
    /// <summary>Szablon do wielokrotnego użycia, np. „Próba z dziećmi”.</summary>
    Template,
}

/// <summary>Plan (lista pozycji) na wydarzenie albo szablon.</summary>
public sealed record Plan(
    Guid Id,
    string Name,
    DateOnly? Date,
    PlanKind Kind,
    IReadOnlyList<PlanItem> Items,
    DateTimeOffset UpdatedAt)
{
    public static Plan Create(string name, DateOnly? date, PlanKind kind = PlanKind.Event) =>
        new(Guid.NewGuid(), name, date, kind, [], DateTimeOffset.Now);

    /// <summary>Kopia z nowymi identyfikatorami — „Użyj ponownie” albo „Zapisz jako szablon”.</summary>
    public Plan CloneAs(string name, DateOnly? date, PlanKind kind) =>
        new(Guid.NewGuid(), name, date, kind, Items.Select(i => i.WithNewId()).ToList(), DateTimeOffset.Now);

    public Plan WithItems(IReadOnlyList<PlanItem> items) => this with { Items = items, UpdatedAt = DateTimeOffset.Now };

    public Plan Insert(int index, PlanItem item)
    {
        var items = Items.ToList();
        items.Insert(Math.Clamp(index, 0, items.Count), item);
        return WithItems(items);
    }

    public Plan Remove(Guid itemId) => WithItems(Items.Where(i => i.Id != itemId).ToList());

    public Plan Replace(PlanItem item) => WithItems(Items.Select(i => i.Id == item.Id ? item : i).ToList());

    public Plan Move(Guid itemId, int newIndex)
    {
        var items = Items.ToList();
        var item = items.Single(i => i.Id == itemId);
        items.Remove(item);
        items.Insert(Math.Clamp(newIndex, 0, items.Count), item);
        return WithItems(items);
    }

    public int IndexOf(Guid itemId) => Items.ToList().FindIndex(i => i.Id == itemId);
}

/// <summary>
/// Pozycja planu. Dziś tylko pieśń; w przyszłości np. stałe elementy (ogłoszenie, modlitwa, obraz).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SongPlanItem), "song")]
public abstract record PlanItem(Guid Id)
{
    public abstract PlanItem WithNewId();
}

/// <summary>Pieśń w planie z wybranymi częściami w wybranej kolejności.</summary>
/// <param name="Arrangement">Kody sekcji do zaśpiewania; <c>null</c> = kolejność z pliku pieśni.</param>
public sealed record SongPlanItem(Guid Id, string SongId, IReadOnlyList<string>? Arrangement) : PlanItem(Id)
{
    public static SongPlanItem For(string songId) => new(Guid.NewGuid(), songId, null);

    public override PlanItem WithNewId() => this with { Id = Guid.NewGuid() };
}
