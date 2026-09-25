using Tailcat.Link;
using Tailcat.Link.Storage;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Link;

public enum RemoteServerStatus
{
    Off,
    Starting,
    Ready,
    Failed,
}

/// <summary>Zaproszenie do sparowania telefonu/przeglądarki: kod i gotowy adres strony (do kodu QR).</summary>
public sealed record RemoteInvitation(string Code, Uri PageUrl, DateTimeOffset ExpiresAt);

public interface IRemoteServer : IAsyncDisposable
{
    RemoteServerStatus Status { get; }

    int ConnectedDevices { get; }

    string? LastError { get; }

    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task<RemoteInvitation> InviteAsync(CancellationToken cancellationToken = default);
}

public sealed record RemoteServerOptions(string AppName, string StateDirectory, Uri PageUrl, int MaxDevices = 8);

/// <summary>
/// Host tailcat-link: przyjmuje polecenia od sparowanych urządzeń i rozsyła im stan ekranu oraz plan.
/// </summary>
public sealed class LinkRemoteServer(
    RemoteServerOptions options,
    RemoteCommandHandler handler,
    ILiveStateSource live,
    ActivePlan plan) : IRemoteServer
{
    private ILinkHost? _host;

    public RemoteServerStatus Status { get; private set; } = RemoteServerStatus.Off;

    public int ConnectedDevices => _host?.Peers.Count(p => p.IsConnected) ?? 0;

    public string? LastError { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_host is not null)
            return;
        SetStatus(RemoteServerStatus.Starting);
        try
        {
            _host = await TailcatLink.HostManyAsync(options.AppName, new LinkOptions
            {
                Store = new FileLinkStore(options.StateDirectory),
                MaxPeers = options.MaxDevices,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            SetStatus(RemoteServerStatus.Failed);
            return;
        }

        _host.SetRequestHandler((_, request, _) => HandleAsync(request));
        _host.PeerJoined += OnPeerJoined;
        _host.PeerLeft += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        live.StateChanged += (_, state) => Broadcast(StateMessage.From(state));
        plan.Changed += (_, _) => Broadcast(handler.CurrentPlan());
        SetStatus(RemoteServerStatus.Ready);
    }

    public async Task<RemoteInvitation> InviteAsync(CancellationToken cancellationToken = default)
    {
        if (_host is null)
            throw new InvalidOperationException("Serwer połączeń nie działa.");
        var invitation = await _host.InviteAsync(new InvitationRequest
        {
            Label = "telefon",
            Lifetime = TimeSpan.FromMinutes(10),
            SingleUse = true,
        }, cancellationToken);
        var code = invitation.Code.Value;
        return new RemoteInvitation(code, new Uri(options.PageUrl, "#code=" + Uri.EscapeDataString(code)), invitation.ExpiresAt);
    }

    private async Task<ReadOnlyMemory<byte>> HandleAsync(ReadOnlyMemory<byte> request) =>
        await handler.HandleAsync(request);

    private void OnPeerJoined(object? sender, PeerEventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        _ = SendAsync(e.Peer, handler.CurrentPlan());
        _ = SendAsync(e.Peer, StateMessage.From(live.State));
    }

    private void Broadcast<T>(T message)
    {
        if (_host is null)
            return;
        foreach (var peer in _host.Peers.Where(p => p.IsConnected))
            _ = SendAsync(peer, message);
    }

    private static async Task SendAsync<T>(ILinkPeer peer, T message)
    {
        try
        {
            await peer.NotifyAsync(RemoteJson.Serialize(message));
        }
        catch (Exception)
        {
            // Urządzenie się rozłączyło — dostanie pełny stan przy ponownym połączeniu (OnPeerJoined).
        }
    }

    private void SetStatus(RemoteServerStatus status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
        _host = null;
    }
}
