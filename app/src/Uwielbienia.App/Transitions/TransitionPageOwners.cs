using System.Runtime.CompilerServices;
using Avalonia;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Które przejście (<see cref="TransitionLog.NextId"/>) ostatnio objęło stronę. Przerwane przejście odsłania stronę
/// tylko, gdy nadal jest jej właścicielem — inaczej nowa strona mignęłaby przed swoim efektem.
/// </summary>
internal static class TransitionPageOwners
{
    private static readonly ConditionalWeakTable<Visual, Owner> Owners = new();

    public static void Claim(Visual? page, int transitionId)
    {
        if (page is not null)
            Owners.AddOrUpdate(page, new Owner(transitionId));
    }

    public static bool Owns(Visual page, int transitionId) =>
        Owners.TryGetValue(page, out var owner) && owner.TransitionId == transitionId;

    private sealed record Owner(int TransitionId);
}
