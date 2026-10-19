using Avalonia.Media.Imaging;
using Uwielbienia.Link;

namespace Uwielbienia.App.Services;

/// <summary>
/// Podglądy obrazów dla telefonu: zmniejszone (JPEG, 480 px — kilkadziesiąt kB) i zapamiętane — kiosk zmienia elementy w kółko, więc ten
/// sam obraz idzie do telefonu wiele razy.
/// </summary>
public sealed class ImagePreviews : IImagePreviews
{
    private const int Width = 480;
    private const int Capacity = 40;

    private readonly Dictionary<string, Task<string?>> _cache = [];
    private readonly LinkedList<string> _order = [];

    public Task<string?> DataUrlAsync(string file)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(file, out var cached))
                return cached;
            var task = Task.Run(() => Encode(file));
            _cache[file] = task;
            _order.AddFirst(file);
            while (_order.Count > Capacity)
            {
                _cache.Remove(_order.Last!.Value);
                _order.RemoveLast();
            }
            return task;
        }
    }

    private static string? Encode(string file)
    {
        try
        {
            using var input = File.OpenRead(file);
            using var bitmap = Bitmap.DecodeToWidth(input, Width, BitmapInterpolationMode.HighQuality);
            using var output = new MemoryStream();
            bitmap.Save(output, new JpegBitmapEncoderOptions { Quality = 75 });
            return "data:image/jpeg;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch (Exception)
        {
            // uszkodzony albo usunięty plik — telefon pokaże sam napis
            return null;
        }
    }
}
