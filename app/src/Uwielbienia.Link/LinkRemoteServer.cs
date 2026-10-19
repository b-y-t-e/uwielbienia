using Tailcat.Link;
using Tailcat.Link.Storage;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Videos;

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

/// <summary>Sparowane urządzenie w oknie „Telefon”: nazwa (tylko podpowiedź), połączenie i uprawnienia.</summary>
public sealed record RemoteDevice(string Key, string Name, DateTimeOffset PairedAt, bool IsConnected, bool CanAddSongs);

/// <summary>Uprawnienia sparowanych urządzeń i ich zapominanie (wiersz urządzenia w oknie „Telefon”).</summary>
public interface IRemoteDeviceManager
{
    /// <summary>Pozwala urządzeniu dodawać pieśni do planu (albo odbiera) — telefon dostaje to od razu.</summary>
    void SetCanAddSongs(string deviceKey, bool allowed);

    /// <summary>Rozparowuje urządzenie: rozłącza je i odrzuca, dopóki nie zeskanuje nowego kodu.</summary>
    Task ForgetAsync(string deviceKey, CancellationToken cancellationToken = default);
}

public interface IRemoteServer : IRemoteDeviceManager, IAsyncDisposable
{
    RemoteServerStatus Status { get; }

    int ConnectedDevices { get; }

    string? LastError { get; }

    /// <summary>Sparowane urządzenia (także niepołączone), od najstarszego.</summary>
    IReadOnlyList<RemoteDevice> Devices { get; }

    /// <summary>Ostatnia nieudana zmiana na liście urządzeń (uprawnienie, „Zapomnij”); <c>null</c> po udanej.</summary>
    string? DevicesError { get; }

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
    ActivePlan plan,
    IDevicePermissions permissions,
    IVideoPlayer? video = null,
    KioskRotation? kiosk = null) : IRemoteServer
{
    private readonly IDevicePermissions _permissions = permissions;
    private ILinkHost? _host;
    private VideoMessage? _lastVideo;
    private DateTime _lastVideoSent;

    public RemoteServerStatus Status { get; private set; } = RemoteServerStatus.Off;

    public int ConnectedDevices => _host?.Peers.Count(p => p.IsConnected) ?? 0;

    public string? LastError { get; private set; }

    public IReadOnlyList<RemoteDevice> Devices => _host?.Peers
        .OrderBy(p => p.PairedAt)
        .Select(p => new RemoteDevice(KeyOf(p), p.Name ?? "urządzenie", p.PairedAt, p.IsConnected, _permissions.CanAddSongs(KeyOf(p))))
        .ToList() ?? [];

    public event EventHandler? Changed;

    public string? DevicesError { get; private set; }

    public void SetCanAddSongs(string deviceKey, bool allowed)
    {
        DevicesError = _permissions.SetCanAddSongs(deviceKey, allowed) ? null : PermissionsNotSaved;
        if (DevicesError is null && _host?.Peers.FirstOrDefault(p => KeyOf(p) == deviceKey) is { IsConnected: true } peer)
            _ = SendAsync(peer, DeviceMessage.From(CallerOf(peer)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ForgetAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        if (_host?.Peers.FirstOrDefault(p => KeyOf(p) == deviceKey) is not { } peer)
            return;
        // najpierw uprawnienie: po ponownym sparowaniu urządzenie zaczyna od zera — tylko zmiana slajdów
        DevicesError = _permissions.SetCanAddSongs(deviceKey, false) ? null : PermissionsNotSaved;
        if (DevicesError is null)
            DevicesError = await TryForgetPeerAsync(_host, peer, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private const string PermissionsNotSaved = "Nie udało się zapisać uprawnień urządzenia.";

    /// <summary>Błąd zapisu stanu połączeń nie może zamknąć aplikacji w trakcie nabożeństwa — wraca jako komunikat.</summary>
    private static async Task<string?> TryForgetPeerAsync(ILinkHost host, ILinkPeer peer, CancellationToken cancellationToken)
    {
        try
        {
            await host.ForgetPeerAsync(peer, cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Nie udało się zapomnieć urządzenia: {ex.Message}";
        }
    }

    private static string KeyOf(ILinkPeer peer) => peer.Key.ToString();

    private RemoteCaller CallerOf(ILinkPeer peer) => new(_permissions.CanAddSongs(KeyOf(peer)));

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

        _host.SetRequestHandler((peer, request, _) => HandleAsync(peer, request));
        _host.PeerJoined += OnPeerJoined;
        _host.PeerLeft += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        live.StateChanged += (_, state) => Broadcast(StateMessage.From(state));
        plan.Changed += (_, _) => Broadcast(handler.CurrentPlan());
        if (video is not null)
            video.StatusChanged += (_, _) => BroadcastVideo();
        if (kiosk is not null)
            kiosk.Changed += (_, _) => _ = BroadcastKioskAsync();
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

    private async Task<ReadOnlyMemory<byte>> HandleAsync(ILinkPeer peer, ReadOnlyMemory<byte> request) =>
        await handler.HandleAsync(request, CallerOf(peer));

    /// <summary>
    /// Stan filmu do telefonów: od razu przy pauzie, starcie, końcu, błędzie i każdej zmianie miejsca zatrzymanego filmu
    /// (±15 s, suwak — inaczej telefon zostałby ze starym czasem, bo nic więcej nie przyjdzie); czas grającego filmu
    /// najwyżej raz na sekundę (następna sekunda i tak go poprawi).
    /// </summary>
    private void BroadcastVideo()
    {
        if (handler.CurrentVideo() is not { } message)
            return;
        var onlyTime = _lastVideo is not null && message with { Position = _lastVideo.Position } == _lastVideo;
        if (onlyTime && message.IsPlaying && DateTime.UtcNow - _lastVideoSent < TimeSpan.FromSeconds(1))
            return;
        _lastVideo = message;
        _lastVideoSent = DateTime.UtcNow;
        Broadcast(message);
    }

    private int _kioskVersion;

    /// <summary>Nowy element kiosku do telefonów; podgląd obrazu powstaje w tle — starszy, gotowy później, nie nadpisuje nowszego.</summary>
    private async Task BroadcastKioskAsync()
    {
        var version = ++_kioskVersion;
        if (await handler.CurrentKioskAsync() is { } message && version == _kioskVersion)
            Broadcast(message);
    }

    private void OnPeerJoined(object? sender, PeerEventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        _ = SendAsync(e.Peer, handler.CurrentPlan());
        _ = SendAsync(e.Peer, StateMessage.From(live.State));
        _ = SendAsync(e.Peer, DeviceMessage.From(CallerOf(e.Peer)));
        if (handler.CurrentVideo() is { } videoState)
            _ = SendAsync(e.Peer, videoState);
        _ = SendKioskAsync(e.Peer);
    }

    private async Task SendKioskAsync(ILinkPeer peer)
    {
        if (await handler.CurrentKioskAsync() is { } message)
            await SendAsync(peer, message);
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
