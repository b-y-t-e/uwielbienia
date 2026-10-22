namespace Uwielbienia.Link;

/// <summary>
/// Wydane zaproszenia (jednorazowe) — rozpoznaje, czy nowe urządzenie sparowało się kodem panelu. Tailcat nie mówi,
/// którym zaproszeniem urządzenie się sparowało: niewygasłe zaproszenie, którego nie ma już wśród żywych, gdy pojawia
/// się nowe urządzenie, zostało użyte właśnie przez nie. Gdy zniknęło też zaproszenie pilota albo kilka naraz, nie
/// wiadomo, kto którym się sparował — wtedy urządzenie nie jest uznane za panel (operator nada uprawnienie ręcznie).
/// </summary>
public sealed class PairingInvitations
{
    private readonly Dictionary<Guid, (RemoteClient Client, DateTimeOffset ExpiresAt)> _issued = [];

    public void Issued(Guid id, RemoteClient client, DateTimeOffset expiresAt) => _issued[id] = (client, expiresAt);

    /// <summary>Nowe urządzenie: czy to panel. Zapomina zaproszenia, których już nie ma.</summary>
    public bool NewDeviceIsPanel(IReadOnlySet<Guid> live, DateTimeOffset now)
    {
        var gone = _issued.Where(i => !live.Contains(i.Key)).ToList();
        foreach (var invitation in gone)
            _issued.Remove(invitation.Key);
        var used = gone.Where(i => i.Value.ExpiresAt > now).Select(i => i.Value.Client).ToList();
        return used is [RemoteClient.Panel];
    }
}
