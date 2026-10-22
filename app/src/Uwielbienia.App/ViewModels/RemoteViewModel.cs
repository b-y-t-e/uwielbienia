using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Link;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Parowanie przez tailcat-link: telefon (pilot, przeglądarka jako drugi ekran) albo panel na drugim komputerze lub
/// tablecie (plany, plan, pieśń, ekran — jak w oknie operatora).
/// </summary>
public sealed partial class RemoteViewModel : ObservableObject
{
    private readonly IRemoteServer _server;

    public RemoteViewModel(IRemoteServer server)
    {
        _server = server;
        _server.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial Bitmap? QrCode { get; private set; }

    [ObservableProperty]
    public partial string? PageUrl { get; private set; }

    [ObservableProperty]
    public partial string? ExpiresText { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Kod prowadzi do panelu (drugi komputer, tablet) zamiast pilota w telefonie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForPilot), nameof(Hint))]
    public partial bool ForPanel { get; private set; }

    public bool ForPilot => !ForPanel;

    public string Hint => ForPanel
        ? "Na drugim komputerze lub tablecie zeskanuj kod albo otwórz adres spod kodu. Otworzy się panel: plany, plan, pieśni i to, co jest na ekranie — wszystko jak tutaj."
        : "Zeskanuj kod aparatem telefonu. Otworzy się pilot: tekst z akordami, Dalej i Wstecz. Na komputerze otwórz ten sam adres i wybierz „Ekran”, żeby pokazywać tekst w przeglądarce.";

    public string StatusText => _server.Status switch
    {
        RemoteServerStatus.Off => "Połączenia wyłączone",
        RemoteServerStatus.Starting => "Łączenie z serwerem pośredniczącym…",
        RemoteServerStatus.Failed => $"Nie udało się uruchomić połączeń: {_server.LastError}",
        _ => _server.ConnectedDevices switch
        {
            0 => "Brak połączonych urządzeń",
            1 => "Połączone: 1 urządzenie",
            var n => $"Połączone urządzenia: {n}",
        },
    };

    public string BadgeText => _server.ConnectedDevices > 0 ? $"Urządzenia: {_server.ConnectedDevices}" : "Urządzenia";

    public bool HasDevices => _server.ConnectedDevices > 0;

    /// <summary>Sparowane urządzenia z uprawnieniem „Dodaje pieśni”.</summary>
    public ObservableCollection<RemoteDeviceViewModel> Devices { get; } = [];

    public bool HasPairedDevices => Devices.Count > 0;

    public string? DevicesError => _server.DevicesError;

    /// <summary>Uruchamia połączenia w tle przy starcie — sparowane wcześniej telefony łączą się same.</summary>
    public async Task StartAsync() => await _server.StartAsync();

    [RelayCommand]
    private async Task Show()
    {
        IsOpen = true;
        if (QrCode is null)
            await Invite();
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private async Task ShowPilot() => await Switch(panel: false);

    [RelayCommand]
    private async Task ShowPanel() => await Switch(panel: true);

    private async Task Switch(bool panel)
    {
        if (ForPanel == panel && QrCode is not null)
            return;
        ForPanel = panel;
        QrCode = null;
        PageUrl = null;
        ExpiresText = null;
        await Invite();
    }

    [RelayCommand]
    private async Task Invite()
    {
        IsBusy = true;
        try
        {
            await _server.StartAsync();
            if (_server.Status != RemoteServerStatus.Ready)
                return;
            var invitation = await _server.InviteAsync(ForPanel ? RemoteClient.Panel : RemoteClient.Pilot);
            PageUrl = invitation.PageUrl.ToString();
            QrCode = QrCodeRenderer.Render(PageUrl);
            ExpiresText = $"Kod ważny do {invitation.ExpiresAt.ToLocalTime():HH:mm} i tylko dla jednego urządzenia.";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        Devices.Clear();
        foreach (var device in _server.Devices)
            Devices.Add(new RemoteDeviceViewModel(device, _server));
        OnPropertyChanged(nameof(HasPairedDevices));
        OnPropertyChanged(nameof(DevicesError));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(HasDevices));
    }
}

/// <summary>
/// Wiersz urządzenia w oknie „Telefon”; przełącznik od razu zmienia uprawnienie (telefon pokazuje albo chowa „+”).
/// „Zapomnij” pyta raz („Zapomnieć?”), drugie kliknięcie rozparowuje.
/// </summary>
public sealed partial class RemoteDeviceViewModel(RemoteDevice device, IRemoteDeviceManager server) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsConfirmingForget { get; private set; }

    [RelayCommand]
    private async Task Forget()
    {
        if (!IsConfirmingForget)
        {
            IsConfirmingForget = true;
            return;
        }
        await server.ForgetAsync(device.Key);
    }

    public string Name => device.Name;

    public bool IsConnected => device.IsConnected;

    public string Details => (device.IsConnected ? "połączone" : "niepołączone") +
        $" · sparowane {device.PairedAt.ToLocalTime():d MMM}";

    public bool CanAddSongs
    {
        get => device.CanAddSongs;
        set => server.SetPermission(device.Key, DevicePermission.AddSongs, value);
    }

    /// <summary>Panel: pełne sterowanie planami i planem (przy panelu „Dodaje pieśni” nie ma znaczenia).</summary>
    public bool IsPanel
    {
        get => device.IsPanel;
        set => server.SetPermission(device.Key, DevicePermission.Panel, value);
    }

    public bool IsPilot => !device.IsPanel;
}
