using Uwielbienia.Core.Plans;

namespace Uwielbienia.Core.Presentation;

/// <summary>Rodzaj pozycji na liście planu — decyduje o znaczniku i kolorze wiersza.</summary>
public enum PlanItemKind
{
    /// <summary>Pieśń ze śpiewnika (numer).</summary>
    Song,
    /// <summary>Tekst: część Mszy, modlitwa, ogłoszenie (kropka zamiast numeru).</summary>
    Text,
    /// <summary>Prezentacja: slajdy-obrazy (znak slajdu zamiast numeru).</summary>
    Presentation,
}

/// <summary>Jak pozycja wygląda na liście planu.</summary>
/// <param name="Number">Numer w śpiewniku; <c>null</c> = bez numeru.</param>
/// <param name="Note">Krótka uwaga pod tytułem, np. „Przygotowywanie slajdów…”.</param>
public sealed record PlanItemInfo(string Title, int? Number, PlanItemKind Kind, string? Note = null);

/// <summary>
/// Jeden rodzaj pozycji planu (pieśń, …): opis na liście i zamiana na to, co pokazuje ekran.
/// Nowy rodzaj elementu prezentacji = nowa implementacja zarejestrowana w <c>AppComposition</c>
/// (opis kroków w CLAUDE.md, „Nowy rodzaj elementu”).
/// </summary>
public interface IPlanItemType
{
    bool Handles(PlanItem item);

    /// <summary>Opis na liście planu; <c>null</c> = pozycja nieaktualna (np. usunięta pieśń) i jest pomijana.</summary>
    PlanItemInfo? Describe(PlanItem item);

    /// <summary>Slajdy do wyświetlenia; <c>null</c> = nie ma czego pokazać.</summary>
    LiveItem? Present(PlanItem item);

    /// <summary>Treść pozycji tego rodzaju zmieniła się (edycja pieśni, gotowe slajdy) — plan przebuduje kolejkę.</summary>
    event EventHandler? Changed;
}

/// <summary>Wszystkie rodzaje pozycji planu — plan, ekran i telefon pytają tylko tę usługę.</summary>
public interface IPlanItemTypes
{
    PlanItemInfo? Describe(PlanItem item);

    LiveItem? Present(PlanItem item);

    /// <summary>Zmieniła się treść pozycji któregoś rodzaju.</summary>
    event EventHandler? Changed;
}

public sealed class PlanItemTypes : IPlanItemTypes
{
    private readonly IReadOnlyList<IPlanItemType> _types;

    public PlanItemTypes(IEnumerable<IPlanItemType> types)
    {
        _types = types.ToList();
        foreach (var type in _types)
            type.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public PlanItemInfo? Describe(PlanItem item) => TypeOf(item)?.Describe(item);

    public LiveItem? Present(PlanItem item) => TypeOf(item)?.Present(item);

    private IPlanItemType? TypeOf(PlanItem item) => _types.FirstOrDefault(t => t.Handles(item));
}
