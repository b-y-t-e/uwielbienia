using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Link;

/// <summary>
/// Obrazy slajdów (prezentacje, obrazy) dla telefonu i panelu: stan ekranu niesie tylko krótki klucz obrazu, a strona
/// prosi o sam obraz poleceniem <c>image</c> — tylko wtedy, gdy go pokazuje, w potrzebnej wielkości. Strona nie poda
/// ścieżki — host wydaje tylko pliki, dla których sam wydał klucz.
/// </summary>
public interface IRemoteImageKeys
{
    /// <returns><c>null</c> dla slajdu bez obrazu (tekst, film, kiosk — ten ma własną wiadomość).</returns>
    string? KeyOf(Slide? slide);

    /// <returns><c>null</c> dla klucza, którego host nie wydał (albo już unieważnił).</returns>
    string? FileOf(string key);
}

/// <summary>Klucz zmienia się razem z plikiem (eksport slajdów od nowa), więc strona nie pokaże starego obrazu.</summary>
public sealed class RemoteImageKeys : IRemoteImageKeys
{
    private readonly ConcurrentDictionary<string, string> _files = new();
    private readonly ConcurrentDictionary<string, string> _keysByFile = new();

    public string? KeyOf(Slide? slide) => slide is ImageSlide image ? KeyOf(image.ImageFile) : null;

    public string KeyOf(string file)
    {
        var stamp = File.GetLastWriteTimeUtc(file).Ticks;
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{stamp}|{file}")))[..20];
        _files[key] = file;
        // nowy plik pod tą samą ścieżką (eksport od nowa) unieważnia stary klucz — rejestr nie rośnie bez końca
        var previous = _keysByFile.GetValueOrDefault(file);
        _keysByFile[file] = key;
        if (previous is not null && previous != key)
            _files.TryRemove(previous, out _);
        return key;
    }

    public string? FileOf(string key) => _files.GetValueOrDefault(key);
}

/// <summary>Wielkości obrazów slajdów i kiosku wysyłanych do telefonu i panelu.</summary>
public static class RemoteImages
{
    /// <summary>Kilka stałych szerokości — mniej wariantów do zmniejszania i zapamiętania na słabszym komputerze.</summary>
    public const int Thumbnail = 320, Phone = 640, Screen = 1280;

    /// <summary>Podgląd elementu kiosku (wiadomość <c>kiosk</c>) — wielkość jak dotąd.</summary>
    public const int Kiosk = 480;

    /// <summary>Najmniejsza stała szerokość nie mniejsza niż żądana; bez żądania — telefon.</summary>
    public static int WidthFor(int? requested) => requested switch
    {
        null => Phone,
        <= Thumbnail => Thumbnail,
        <= Phone => Phone,
        _ => Screen,
    };
}
