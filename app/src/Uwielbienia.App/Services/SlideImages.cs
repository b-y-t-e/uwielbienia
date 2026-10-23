using Avalonia.Media.Imaging;

namespace Uwielbienia.App.Services;

/// <summary>
/// Obrazy slajdów prezentacji wczytywane w tle (bez zacinania interfejsu) i trzymane w pamięci podręcznej:
/// pełna rozdzielczość dla rzutnika (kilka ostatnich + następny slajd wczytany zawczasu) i miniatury dla operatora.
/// </summary>
public static class SlideImages
{
    private const int ScreenWidth = 1920;
    public const int ThumbnailWidth = 640;

    // Slajd ma najwyżej szerokość płótna rzutnika — zdjęcie z aparatu w pełnej rozdzielczości zajęłoby ~100 MB.
    private static readonly ImageCache Full = new(capacity: 8, file => Decode(file, ScreenWidth));

    private static readonly ImageCache Thumbnails = new(capacity: 160, file => Decode(file, ThumbnailWidth));

    private static Bitmap Decode(string file, int width)
    {
        using var stream = File.OpenRead(file);
        return Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
    }

    public static Task<Bitmap?> LoadAsync(string file) => Full.Get(file);

    public static Task<Bitmap?> LoadThumbnailAsync(string file) => Thumbnails.Get(file);

    /// <summary>Wczytuje zawczasu slajd, który zaraz będzie na ekranie — przejście bez opóźnienia.</summary>
    public static void Preload(string file) => _ = Full.Get(file);

    private sealed class ImageCache(int capacity, Func<string, Bitmap> decode)
    {
        private readonly TaskCache<Bitmap> _items = new(capacity);

        // ponowny eksport zapisuje nowe slajdy pod tymi samymi ścieżkami — klucz z czasem zapisu
        public Task<Bitmap?> Get(string file) =>
            _items.Get($"{file}|{File.GetLastWriteTimeUtc(file).Ticks}", () => Task.Run(() => Decode(file)));

        private Bitmap? Decode(string file)
        {
            try
            {
                return decode(file);
            }
            catch (Exception)
            {
                // uszkodzony albo usunięty plik — slajd bez obrazu zamiast awarii pokazu
                return null;
            }
        }
    }
}
