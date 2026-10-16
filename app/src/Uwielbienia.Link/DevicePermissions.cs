using System.Text.Json;

namespace Uwielbienia.Link;

/// <summary>Kto (urządzenie z sesją połączenia) o to prosi i co mu wolno.</summary>
/// <param name="CanAddSongs">Operator pozwolił temu urządzeniu dodawać pieśni do planu.</param>
public sealed record RemoteCaller(bool CanAddSongs = false)
{
    /// <summary>Zwykły telefon: tylko zmienia slajdy (domyślnie każde urządzenie).</summary>
    public static readonly RemoteCaller Pilot = new();
}

/// <summary>Uprawnienia sparowanych urządzeń według klucza urządzenia (nie nazwy).</summary>
public interface IDevicePermissions
{
    bool CanAddSongs(string deviceKey);

    /// <summary>Zmienia uprawnienie dopiero po udanym zapisie; <c>false</c> = zapis się nie udał, nic się nie zmieniło.</summary>
    bool SetCanAddSongs(string deviceKey, bool allowed);
}

/// <summary>
/// Uprawnienia sparowanych urządzeń (klucz publiczny urządzenia — jedyne, co połączenie uwierzytelnia; nazwa jest tylko
/// podpowiedzią). Domyślnie telefon tylko zmienia slajdy; dodawanie pieśni operator włącza w oknie „Telefon”.
/// Zapis w <c>polaczenia/uprawnienia.json</c>.
/// </summary>
public sealed class DevicePermissions : IDevicePermissions
{
    private readonly string? _file;
    private readonly Lock _lock = new();
    private HashSet<string> _canAddSongs;

    public DevicePermissions(string? stateDirectory = null)
    {
        _file = stateDirectory is null ? null : Path.Combine(stateDirectory, "uprawnienia.json");
        _canAddSongs = Load(_file);
    }

    public bool CanAddSongs(string deviceKey)
    {
        lock (_lock)
            return _canAddSongs.Contains(deviceKey);
    }

    public bool SetCanAddSongs(string deviceKey, bool allowed)
    {
        lock (_lock)
        {
            var changed = new HashSet<string>(_canAddSongs);
            if (!(allowed ? changed.Add(deviceKey) : changed.Remove(deviceKey)))
                return true;
            if (!TrySave(changed))
                return false;
            _canAddSongs = changed;
            return true;
        }
    }

    private bool TrySave(HashSet<string> canAddSongs)
    {
        if (_file is null)
            return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(new Stored(canAddSongs.Order().ToList()), RemoteJson.Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Lista może przyjść jako <c>null</c> (plik bez niej) — JSON nie pilnuje adnotacji.</summary>
    private sealed record Stored(List<string>? CanAddSongs);

    private static HashSet<string> Load(string? file)
    {
        try
        {
            if (file is not null && File.Exists(file) &&
                JsonSerializer.Deserialize<Stored>(File.ReadAllText(file), RemoteJson.Options) is { CanAddSongs: { } canAddSongs })
                return [.. canAddSongs];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // uszkodzony plik = nikt nie może dodawać (bezpieczniej niż wszyscy)
        }
        return [];
    }
}
