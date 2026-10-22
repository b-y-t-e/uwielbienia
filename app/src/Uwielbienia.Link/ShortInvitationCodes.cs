namespace Uwielbienia.Link;

/// <summary>
/// Krótki kod parowania do podyktowania (np. „ajk59”): usługa <c>kod.php</c> na stronie pilota zapamiętuje długi kod
/// tailcat-link na <see cref="Lifetime"/> i oddaje go raz stronie, która poda krótki. Szyfrowanie i jednorazowość
/// zaproszenia zostają bez zmian — krótki kod tylko je wskazuje.
/// </summary>
public static class ShortInvitationCodes
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>Krótki kod albo <c>null</c>, gdy usługa nie odpowiedziała — wtedy zostaje kod QR i długi adres.</summary>
    public static async Task<string?> RegisterAsync(Uri service, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.PostAsync(service, new StringContent(code), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var shortCode = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            return shortCode.Length is > 0 and <= 8 && shortCode.All(char.IsAsciiLetterOrDigit) ? shortCode : null;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
