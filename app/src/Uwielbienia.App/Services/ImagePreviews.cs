using Avalonia;
using Avalonia.Media.Imaging;
using Uwielbienia.Link;

namespace Uwielbienia.App.Services;

/// <summary>
/// Podglądy obrazów dla telefonu i panelu: zmniejszone (JPEG, kilka stałych szerokości — kilkadziesiąt kB) i zapamiętane —
/// kiosk zmienia elementy w kółko, a slajdy prezentacji ogląda kilka urządzeń, więc ten sam obraz idzie wiele razy.
/// Zmniejsza tylko na żądanie urządzenia, po jednym obrazie naraz, w tle i z obrazu już wczytanego przez aplikację
/// (<see cref="SlideImages"/>) — starszy komputer nie zwalnia w trakcie nabożeństwa; każdy obraz w każdej wielkości raz.
/// Zmieniony plik (eksport slajdów od nowa) to nowy podgląd; nieudany nie zostaje w pamięci (np. plik chwilowo zajęty).
/// </summary>
public sealed class ImagePreviews : IImagePreviews
{
    private const int Capacity = 120;

    private readonly TaskCache<string> _cache = new(Capacity);
    // większy podgląd (bieżący slajd telefonu, ekran) przed miniaturami panelu
    private readonly PriorityGate _oneAtATime = new();

    public Task<string?> DataUrlAsync(string file, int width = RemoteImages.Phone) =>
        _cache.Get($"{width}|{File.GetLastWriteTimeUtc(file).Ticks}|{file}",
            () => _oneAtATime.RunAsync(width, () => EncodeAsync(file, width)));

    private static async Task<string?> EncodeAsync(string file, int width)
    {
        // obraz, który aplikacja i tak trzyma w pamięci (miniatura operatora albo slajd rzutnika) — duży PNG
        // nie jest dekodowany drugi raz, zostaje tylko zmniejszenie i JPEG
        var source = width > SlideImages.ThumbnailWidth
            ? await SlideImages.LoadAsync(file)
            : await SlideImages.LoadThumbnailAsync(file);
        return source is null ? null : await Task.Run(() => Encode(source, width));
    }

    private static readonly JpegBitmapEncoderOptions Jpeg = new() { Quality = 75 };

    private static string? Encode(Bitmap source, int width)
    {
        try
        {
            using var output = new MemoryStream();
            if (source.PixelSize.Width > width)
            {
                var height = Math.Max(1, (int)Math.Round(source.PixelSize.Height * (double)width / source.PixelSize.Width));
                using var scaled = source.CreateScaledBitmap(new PixelSize(width, height), BitmapInterpolationMode.HighQuality);
                scaled.Save(output, Jpeg);
            }
            else
                source.Save(output, Jpeg);
            return "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch (Exception)
        {
            // obraz, którego nie da się zapisać — telefon pokaże sam napis / nazwę slajdu
            return null;
        }
    }
}
