using Avalonia.Media.Imaging;
using QRCoder;

namespace Uwielbienia.App.Services;

public static class QrCodeRenderer
{
    public static Bitmap Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(12, [27, 33, 56], [255, 255, 255], drawQuietZones: true);
        using var stream = new MemoryStream(png);
        return new Bitmap(stream);
    }
}
