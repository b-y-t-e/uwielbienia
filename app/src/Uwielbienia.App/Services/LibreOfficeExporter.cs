using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Docnet.Core;
using Docnet.Core.Models;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.Services;

/// <summary>
/// Slajdy przez LibreOffice (Linux, Windows bez PowerPointa i DeckCraftu): <c>soffice --headless --convert-to pdf</c>
/// (osobny profil — nie koliduje z otwartym LibreOffice; slajdy ukryte LibreOffice pomija), potem strony PDF
/// jako PNG przez PDFium (Docnet).
/// </summary>
public sealed class LibreOfficeExporter : ISlideExporter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly Lazy<string?> _soffice = new(() => ExternalTool.Find(
        [
            ExternalTool.ProgramFiles("LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/opt/libreoffice/program/soffice",
            "/snap/bin/libreoffice",
            "/var/lib/flatpak/exports/bin/org.libreoffice.LibreOffice",
        ],
        "soffice", "libreoffice"));

    public string Name => "LibreOffice";

    public bool IsAvailable => _soffice.Value is not null;

    public async Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
    {
        var soffice = _soffice.Value ?? throw new InvalidOperationException("Brak programu LibreOffice");
        var work = Path.Combine(Path.GetTempPath(), "uwielbienia-lo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            progress.Report("Slajdy (LibreOffice)…");
            await ExternalTool.RunAsync(soffice,
            [
                "-env:UserInstallation=" + new Uri(Path.Combine(work, "profil")).AbsoluteUri,
                "--headless", "--convert-to", "pdf", "--outdir", work, presentation,
            ], Timeout).ConfigureAwait(false);
            var pdf = Directory.EnumerateFiles(work, "*.pdf").FirstOrDefault()
                      ?? throw new InvalidOperationException("LibreOffice nie zapisał slajdów");
            await Task.Run(() => RenderPages(pdf, outDir, width, progress)).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // profil LibreOffice bywa jeszcze chwilę zajęty — zostaje w katalogu tymczasowym
            }
        }
    }

    private static void RenderPages(string pdf, string outDir, int width, IProgress<string> progress)
    {
        double pageWidth;
        using (var probe = DocLib.Instance.GetDocReader(pdf, new PageDimensions(1.0)))
        using (var first = probe.GetPageReader(0))
            pageWidth = first.GetPageWidth();

        using var doc = DocLib.Instance.GetDocReader(pdf, new PageDimensions(width / pageWidth));
        var count = doc.GetPageCount();
        for (var i = 0; i < count; i++)
        {
            progress.Report($"Slajd {i + 1} z {count}…");
            using var page = doc.GetPageReader(i);
            SavePng(page.GetImage(), page.GetPageWidth(), page.GetPageHeight(), Path.Combine(outDir, $"slide{i + 1:D3}.png"));
        }
    }

    /// <summary>Piksele BGRA z PDFium (przezroczyste tło) na białym tle jako PNG.</summary>
    private static void SavePng(byte[] bgra, int width, int height, string path)
    {
        for (var p = 0; p < bgra.Length; p += 4)
        {
            var alpha = bgra[p + 3];
            if (alpha == 255)
                continue;
            for (var c = 0; c < 3; c++)
                bgra[p + c] = (byte)((bgra[p + c] * alpha + 255 * (255 - alpha)) / 255);
            bgra[p + 3] = 255;
        }

        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
            for (var y = 0; y < height; y++)
                Marshal.Copy(bgra, y * width * 4, buffer.Address + y * buffer.RowBytes, width * 4);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }
}
