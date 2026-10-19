using Uwielbienia.Core.Kiosks;

namespace Uwielbienia.Link;

/// <summary>Mały podgląd obrazu dla telefonu (data URL), np. bieżącego elementu kiosku.</summary>
public interface IImagePreviews
{
    /// <returns><c>null</c>, gdy obrazu nie da się odczytać.</returns>
    Task<string?> DataUrlAsync(string file);
}

/// <summary>
/// Bieżący element kiosku na ekranie: telefon pokazuje podgląd obrazu i napis. Wysyłane przy każdej zmianie elementu
/// (kiosk na ekranie — <see cref="StateMessage.IsKiosk"/>).
/// </summary>
/// <param name="ItemId">Pozycja planu z tym kioskiem — strona pokazuje element tylko przy stanie ekranu z tą samą pozycją
/// (wiadomość może przyjść przed stanem albo po nim, a stara — po zmianie kiosku).</param>
/// <param name="Image">Podgląd obrazu (data URL); <c>null</c> = element bez obrazu.</param>
public sealed record KioskMessage(Guid? ItemId, int Index, int Count, bool IsRunning, string? Title, string? Text, string? Image)
{
    public string Type => "kiosk";

    public static async Task<KioskMessage?> FromAsync(KioskRotation rotation, IImagePreviews? previews)
    {
        if (rotation.Frame is not { } frame)
            return null;
        var (itemId, index, count, isRunning) = (rotation.ItemId, rotation.Index, rotation.Count, rotation.IsRunning);
        var image = frame.Image is { } file && previews is not null ? await previews.DataUrlAsync(file) : null;
        return new KioskMessage(itemId, index, count, isRunning, frame.Title, frame.Text, image);
    }
}
