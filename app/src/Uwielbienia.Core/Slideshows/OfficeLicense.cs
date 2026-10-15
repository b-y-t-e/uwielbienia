using System.Text;
using System.Text.Json;

namespace Uwielbienia.Core.Slideshows;

/// <summary>
/// Czy Microsoft Office (Microsoft 365) ma ważną licencję — z plików licencji, które Office trzyma lokalnie
/// (<c>%LOCALAPPDATA%\Microsoft\Office\Licenses\5\*</c>: JSON w UTF-16 z polem <c>License</c> = Base64 z JSON-em,
/// w nim <c>ExpiresOn</c>). Po wygaśnięciu subskrypcji PowerPoint działa w trybie ograniczonym i może
/// zatrzymać eksport oknem aktywacji, dlatego wtedy używamy innego sposobu eksportu.
/// </summary>
public static class OfficeLicense
{
    /// <summary>
    /// Ważna, gdy któraś licencja nie wygasła (bez daty = bezterminowa). Brak plików licencji (wersja
    /// pudełkowa, licencja zbiorcza) = nie da się sprawdzić — traktujemy jako ważną.
    /// </summary>
    public static bool IsActive(IEnumerable<byte[]> licenseFiles, DateTimeOffset now)
    {
        var known = false;
        foreach (var file in licenseFiles)
        {
            if (!TryReadExpiry(file, out var expires))
                continue;
            known = true;
            if (expires is null || expires > now)
                return true;
        }
        return !known;
    }

    /// <summary>Data wygaśnięcia z pliku licencji; <c>false</c> = nieczytelny plik.</summary>
    public static bool TryReadExpiry(byte[] file, out DateTimeOffset? expires)
    {
        expires = null;
        try
        {
            // UTF-16 (zwykle bez BOM) albo UTF-8
            var text = file.Length > 1 && file[1] == 0 ? Encoding.Unicode.GetString(file) : Encoding.UTF8.GetString(file);
            using var outer = JsonDocument.Parse(text.Trim('﻿', '\0', ' ', '\r', '\n'));
            if (!outer.RootElement.TryGetProperty("License", out var license) || license.GetString() is not { } encoded)
                return false;
            using var inner = JsonDocument.Parse(Convert.FromBase64String(encoded));
            if (inner.RootElement.TryGetProperty("ExpiresOn", out var expiresOn) && expiresOn.ValueKind == JsonValueKind.String)
                expires = DateTimeOffset.Parse(expiresOn.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or DecoderFallbackException)
        {
            return false;
        }
    }
}
