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

/// <summary>Strona, na którą prowadzi zaproszenie: pilot w telefonie albo panel na drugim komputerze / tablecie.</summary>
public enum RemoteClient
{
    Pilot,
    Panel,
}

/// <summary>
/// Zaproszenie do sparowania telefonu/przeglądarki: kod i gotowy adres strony (do kodu QR). <paramref name="ShortCode"/> —
/// krótki kod do podyktowania (np. „ajk59”, ważny <see cref="ShortInvitationCodes.Lifetime"/>), a <paramref name="ShortPageUrl"/>
/// adres z nim; <c>null</c>, gdy usługa krótkich kodów nie odpowiedziała.
/// </summary>
public sealed record RemoteInvitation(
    string Code, Uri PageUrl, DateTimeOffset ExpiresAt, RemoteClient Client = RemoteClient.Pilot,
    string? ShortCode = null, Uri? ShortPageUrl = null, DateTimeOffset? ShortExpiresAt = null);

/// <summary>Sparowane urządzenie w oknie „Urządzenia”: nazwa (tylko podpowiedź), połączenie i uprawnienia.</summary>
public sealed record RemoteDevice(
    string Key, string Name, DateTimeOffset PairedAt, bool IsConnected, bool CanAddSongs, bool IsPanel = false);

/// <summary>Uprawnienia sparowanych urządzeń i ich zapominanie (wiersz urządzenia w oknie „Urządzenia”).</summary>
public interface IRemoteDeviceManager
{
    /// <summary>Nadaje urządzeniu uprawnienie (albo odbiera) — urządzenie dostaje to od razu.</summary>
    void SetPermission(string deviceKey, DevicePermission permission, bool allowed);

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

    /// <summary>Kod parowania; zaproszenie panelu nadaje urządzeniu uprawnienie „Panel”.</summary>
    Task<RemoteInvitation> InviteAsync(RemoteClient client = RemoteClient.Pilot, CancellationToken cancellationToken = default);
}

/// <param name="PanelUrl">Strona panelu (drugi komputer, tablet); <c>null</c> = obok strony pilota (<c>../panel/</c>).</param>
/// <param name="ShortCodeUrl">Usługa krótkich kodów (<c>kod.php</c>); <c>null</c> = tylko długi kod.</param>
public sealed record RemoteServerOptions(
    string AppName, string StateDirectory, Uri PageUrl, int MaxDevices = 8, Uri? PanelUrl = null, Uri? ShortCodeUrl = null)
{
    public Uri PanelPageUrl => PanelUrl ?? new Uri(PageUrl, "../panel/");
}

/// <summary>
/// Host tailcat-link: przyjmuje polecenia od sparowanych urządzeń i rozsyła im stan ekranu oraz plan (panelom także
/// listę planów).
/// </summary>
public sealed class LinkRemoteServer(
    RemoteServerOptions options,
    RemoteCommandHandler handler,
    ILiveStateSource live,
    ActivePlan plan,
    IDevicePermissions permissions,
    IVideoPlayer? video = null,
    KioskRotation? kiosk = null,
    PlanLibrary? plans = null) : IRemoteServer
{
    private readonly IDevicePermissions _permissions = permissions;
    private readonly Lock _lock = new();
    private ILinkHost? _host;
    private VideoMessage? _lastVideo;
    private DateTime _lastVideoSent;

    /// <summary>Urządzenia już sparowane — nowe porównujemy z zaproszeniami panelu.</summary>
    private readonly HashSet<string> _knownKeys = [];

    private readonly PairingInvitations _invitations = new();

    public RemoteServerStatus Status { get; private set; } = RemoteServerStatus.Off;

    public int ConnectedDevices => _host?.Peers.Count(p => p.IsConnected) ?? 0;

    public string? LastError { get; private set; }

    public IReadOnlyList<RemoteDevice> Devices => _host?.Peers
        .OrderBy(p => p.PairedAt)
        .Select(p => new RemoteDevice(KeyOf(p), p.Name ?? "urządzenie", p.PairedAt, p.IsConnected,
            _permissions.CanAddSongs(KeyOf(p)), _permissions.Has(KeyOf(p), DevicePermission.Panel)))
        .ToList() ?? [];

    public event EventHandler? Changed;

    public string? DevicesError { get; private set; }

    public void SetPermission(string deviceKey, DevicePermission permission, bool allowed)
    {
        DevicesError = _permissions.Set(deviceKey, permission, allowed) ? null : PermissionsNotSaved;
        if (DevicesError is null && _host?.Peers.FirstOrDefault(p => KeyOf(p) == deviceKey) is { IsConnected: true } peer)
            SendWelcome(peer);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ForgetAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        if (_host?.Peers.FirstOrDefault(p => KeyOf(p) == deviceKey) is not { } peer)
            return;
        // najpierw uprawnienia: po ponownym sparowaniu urządzenie zaczyna od zera — tylko zmiana slajdów
        DevicesError = Enum.GetValues<DevicePermission>().All(p => _permissions.Set(deviceKey, p, false))
            ? null
            : PermissionsNotSaved;
        if (DevicesError is null)
        {
            DevicesError = await TryForgetPeerAsync(_host, peer, cancellationToken);
            lock (_lock)
                _knownKeys.Remove(deviceKey);
        }
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

    private RemoteCaller CallerOf(ILinkPeer peer)
    {
        Recognize(peer);
        return _permissions.CallerFor(KeyOf(peer));
    }

    /// <summary>Nowe urządzenie sparowane kodem panelu dostaje uprawnienie „Panel” (<see cref="PairingInvitations"/>).</summary>
    private void Recognize(ILinkPeer peer)
    {
        var key = KeyOf(peer);
        lock (_lock)
        {
            if (_host is null || !_knownKeys.Add(key))
                return;
            if (!_invitations.NewDeviceIsPanel(_host.Invitations.Select(i => i.Id).ToHashSet(), DateTimeOffset.UtcNow))
                return;
        }
        if (!_permissions.Set(key, DevicePermission.Panel, true))
            DevicesError = PermissionsNotSaved;
    }

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

        lock (_lock)
            _knownKeys.UnionWith(_host.Peers.Select(KeyOf));
        _host.SetRequestHandler((peer, request, _) => HandleAsync(peer, request));
        _host.PeerJoined += OnPeerJoined;
        _host.PeerLeft += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        live.StateChanged += (_, state) => Broadcast(StateMessage.From(state));
        plan.Changed += (_, _) => Broadcast(handler.CurrentPlan());
        plan.Opened += (_, _) => BroadcastPlans();
        if (plans is not null)
            plans.Changed += (_, _) => BroadcastPlans();
        if (video is not null)
            video.StatusChanged += (_, _) => BroadcastVideo();
        if (kiosk is not null)
            kiosk.Changed += (_, _) => _ = BroadcastKioskAsync();
        SetStatus(RemoteServerStatus.Ready);
    }

    public async Task<RemoteInvitation> InviteAsync(RemoteClient client = RemoteClient.Pilot, CancellationToken cancellationToken = default)
    {
        if (_host is null)
            throw new InvalidOperationException("Serwer połączeń nie działa.");
        var invitation = await _host.InviteAsync(new InvitationRequest
        {
            Label = client == RemoteClient.Panel ? "panel" : "telefon",
            Lifetime = TimeSpan.FromMinutes(10),
            SingleUse = true,
        }, cancellationToken);
        lock (_lock)
            _invitations.Issued(invitation.Id, client, invitation.ExpiresAt);
        var code = invitation.Code.Value;
        var page = client == RemoteClient.Panel ? options.PanelPageUrl : options.PageUrl;
        var result = new RemoteInvitation(code, new Uri(page, "#code=" + Uri.EscapeDataString(code)), invitation.ExpiresAt, client);
        if (options.ShortCodeUrl is null)
            return result;
        var shortCode = await ShortInvitationCodes.RegisterAsync(options.ShortCodeUrl, code, cancellationToken);
        return shortCode is null ? result : result with
        {
            ShortCode = shortCode,
            ShortPageUrl = new Uri(page, "#code=" + shortCode),
            ShortExpiresAt = DateTimeOffset.Now + ShortInvitationCodes.Lifetime,
        };
    }

    private async Task<ReadOnlyMemory<byte>> HandleAsync(ILinkPeer peer, ReadOnlyMemory<byte> request)
    {
        if (!RemoteCommandHandler.IsLeave(request))
            return await handler.HandleAsync(request, CallerOf(peer));
        // urządzenie samo się odłącza („Odłącz” na stronie): zapominamy je jak „Zapomnij” w oknie „Urządzenia”,
        // chwilę po odpowiedzi — inaczej zerwane połączenie zgubiłoby potwierdzenie
        var key = KeyOf(peer);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await ForgetAsync(key);
        });
        return RemoteJson.Serialize(new CommandReply(true));
    }

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
        SendWelcome(e.Peer);
        if (handler.CurrentVideo() is { } videoState)
            _ = SendAsync(e.Peer, videoState);
        _ = SendKioskAsync(e.Peer);
    }

    /// <summary>Uprawnienia urządzenia, a panelowi — także lista planów.</summary>
    private void SendWelcome(ILinkPeer peer)
    {
        var caller = CallerOf(peer);
        _ = SendAsync(peer, DeviceMessage.From(caller));
        if (caller.IsPanel && handler.CurrentPlans() is { } list)
            _ = SendAsync(peer, list);
    }

    /// <summary>Lista planów tylko do paneli — telefon jej nie potrzebuje.</summary>
    private void BroadcastPlans()
    {
        if (_host is null || handler.CurrentPlans() is not { } list)
            return;
        foreach (var peer in _host.Peers.Where(p => p.IsConnected && _permissions.Has(KeyOf(p), DevicePermission.Panel)))
            _ = SendAsync(peer, list);
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
