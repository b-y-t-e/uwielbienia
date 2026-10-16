using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Link;

namespace Uwielbienia.App.ViewModels;

/// <summary>Parowanie telefonu (pilot) lub przeglądarki (drugi ekran) przez tailcat-link.</summary>
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

    public string BadgeText => _server.ConnectedDevices > 0 ? $"Telefon: {_server.ConnectedDevices}" : "Telefon";

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
    private async Task Invite()
    {
        IsBusy = true;
        try
        {
            await _server.StartAsync();
            if (_server.Status != RemoteServerStatus.Ready)
                return;
            var invitation = await _server.InviteAsync();
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
        set => server.SetCanAddSongs(device.Key, value);
    }
}
