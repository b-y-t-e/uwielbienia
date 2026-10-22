using System.Text.Json;

namespace Uwielbienia.Link;

/// <summary>Kto (urządzenie z sesją połączenia) o to prosi i co mu wolno.</summary>
/// <param name="CanAddSongs">Operator pozwolił temu urządzeniu dodawać pieśni do planu.</param>
/// <param name="IsPanel">Panel (drugi komputer, tablet): plany, plan, układ pieśni i ekran — wszystko jak w oknie operatora.</param>
public sealed record RemoteCaller(bool CanAddSongs = false, bool IsPanel = false)
{
    /// <summary>Zwykły telefon: tylko zmienia slajdy (domyślnie każde urządzenie).</summary>
    public static readonly RemoteCaller Pilot = new();

    public static readonly RemoteCaller Panel = new(CanAddSongs: true, IsPanel: true);
}

/// <summary>Uprawnienie sparowanego urządzenia nadawane przez operatora.</summary>
public enum DevicePermission
{
    /// <summary>Telefon: „+” — pieśń jako następna w planie.</summary>
    AddSongs,
    /// <summary>Panel na drugim komputerze lub tablecie — pełne sterowanie planami i planem.</summary>
    Panel,
}

/// <summary>Uprawnienia sparowanych urządzeń według klucza urządzenia (nie nazwy).</summary>
public interface IDevicePermissions
{
    bool Has(string deviceKey, DevicePermission permission);

    /// <summary>Zmienia uprawnienie dopiero po udanym zapisie; <c>false</c> = zapis się nie udał, nic się nie zmieniło.</summary>
    bool Set(string deviceKey, DevicePermission permission, bool allowed);
}

public static class DevicePermissionsExtensions
{
    public static RemoteCaller CallerFor(this IDevicePermissions permissions, string deviceKey) =>
        permissions.Has(deviceKey, DevicePermission.Panel)
            ? RemoteCaller.Panel
            : new RemoteCaller(CanAddSongs: permissions.Has(deviceKey, DevicePermission.AddSongs));

    public static bool CanAddSongs(this IDevicePermissions permissions, string deviceKey) =>
        permissions.Has(deviceKey, DevicePermission.AddSongs);

    public static bool SetCanAddSongs(this IDevicePermissions permissions, string deviceKey, bool allowed) =>
        permissions.Set(deviceKey, DevicePermission.AddSongs, allowed);
}

/// <summary>
/// Uprawnienia sparowanych urządzeń (klucz publiczny urządzenia — jedyne, co połączenie uwierzytelnia; nazwa jest tylko
/// podpowiedzią). Domyślnie telefon tylko zmienia slajdy; dodawanie pieśni i panel operator włącza w oknie „Telefon”
/// (panel — też samo sparowanie kodem panelu). Zapis w <c>polaczenia/uprawnienia.json</c>.
/// </summary>
public sealed class DevicePermissions : IDevicePermissions
{
    private readonly string? _file;
    private readonly Lock _lock = new();
    private Stored _stored;

    public DevicePermissions(string? stateDirectory = null)
    {
        _file = stateDirectory is null ? null : Path.Combine(stateDirectory, "uprawnienia.json");
        _stored = Load(_file);
    }

    public bool Has(string deviceKey, DevicePermission permission)
    {
        lock (_lock)
            return SetOf(_stored, permission)?.Contains(deviceKey) == true;
    }

    public bool Set(string deviceKey, DevicePermission permission, bool allowed)
    {
        lock (_lock)
        {
            var keys = new SortedSet<string>(SetOf(_stored, permission) ?? [], StringComparer.Ordinal);
            if (!(allowed ? keys.Add(deviceKey) : keys.Remove(deviceKey)))
                return true;
            var changed = permission switch
            {
                DevicePermission.AddSongs => _stored with { CanAddSongs = [.. keys] },
                _ => _stored with { Panels = [.. keys] },
            };
            if (!TrySave(changed))
                return false;
            _stored = changed;
            return true;
        }
    }

    private static List<string>? SetOf(Stored stored, DevicePermission permission) => permission switch
    {
        DevicePermission.AddSongs => stored.CanAddSongs,
        _ => stored.Panels,
    };

    private bool TrySave(Stored stored)
    {
        if (_file is null)
            return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(stored, RemoteJson.Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Listy mogą przyjść jako <c>null</c> (plik bez nich) — JSON nie pilnuje adnotacji.</summary>
    private sealed record Stored(List<string>? CanAddSongs, List<string>? Panels = null);

    private static Stored Load(string? file)
    {
        try
        {
            if (file is not null && File.Exists(file) &&
                JsonSerializer.Deserialize<Stored>(File.ReadAllText(file), RemoteJson.Options) is { } stored)
                return stored;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // uszkodzony plik = nikt nie ma uprawnień (bezpieczniej niż wszyscy)
        }
        return new Stored([], []);
    }
}
