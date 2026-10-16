using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Link;

/// <summary>
/// Pieśni dodane z telefonu, które telefon może jeszcze cofnąć: tylko przez <see cref="UndoTime"/> i tylko póki
/// nie trafiły na ekran (pokazana raz — nawet po zejściu z ekranu — już nie do cofnięcia).
/// </summary>
public sealed class UndoableAdditions
{
    /// <summary>Telefon pokazuje „Cofnij” przez 5 s; zapas na opóźnienie sieci.</summary>
    public static readonly TimeSpan UndoTime = TimeSpan.FromSeconds(10);

    private readonly Dictionary<Guid, DateTime> _addedAt = [];
    private readonly Func<DateTime> _now;

    public UndoableAdditions(ILiveStateSource live, Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.UtcNow);
        live.StateChanged += (_, state) => Forget(state.Item?.PlanItemId);
    }

    public void Add(Guid itemId) => _addedAt[itemId] = _now();

    /// <summary>Zdejmuje dodanie z listy; <c>true</c>, gdy wolno je jeszcze cofnąć.</summary>
    public bool TryTake(Guid itemId) =>
        _addedAt.Remove(itemId, out var addedAt) && _now() - addedAt <= UndoTime;

    private void Forget(Guid? shownItemId)
    {
        if (shownItemId is { } id)
            _addedAt.Remove(id);
    }
}
