using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.Services;

/// <summary>
/// Slajdy prezentacji jako PNG przez zainstalowany PowerPoint (automatyzacja COM, bez okna) — to samo
/// co pps_viewer. Własny wątek STA, bo COM PowerPointa tego wymaga; pomija slajdy ukryte w pokazie.
/// </summary>
public sealed class PowerPointExporter : ISlideExporter
{
    public Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException(
                        "Prezentację otwiera PowerPoint (Windows). Zapisz slajdy jako obrazy i dodaj obrazy.");
                Export(presentation, outDir, width, progress);
                done.SetResult();
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        }) { IsBackground = true };
        if (OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    private const int MsoTrue = -1;
    private const int MsoFalse = 0;

    [SupportedOSPlatform("windows")]
    private static void Export(string path, string outDir, int width, IProgress<string> progress)
    {
        var type = Type.GetTypeFromProgID("PowerPoint.Application")
                   ?? throw new InvalidOperationException("Do prezentacji potrzebny jest PowerPoint. Można też dodać slajdy zapisane jako obrazy.");
        progress.Report("Uruchamianie PowerPointa…");
        dynamic app = Activator.CreateInstance(type)!;
        dynamic? presentation = null;
        try
        {
            // tylko do odczytu, bez tytułu, bez okna
            presentation = app.Presentations.Open(Path.GetFullPath(path), MsoTrue, MsoFalse, MsoFalse);
            double slideWidth = presentation.PageSetup.SlideWidth;
            double slideHeight = presentation.PageSetup.SlideHeight;
            var height = (int)Math.Round(width * slideHeight / slideWidth);

            int count = presentation.Slides.Count;
            var exported = 0;
            for (var i = 1; i <= count; i++)
            {
                dynamic slide = presentation.Slides.Item(i);
                if ((int)slide.SlideShowTransition.Hidden == MsoTrue)
                    continue;
                progress.Report($"Slajd {i} z {count}…");
                exported++;
                slide.Export(Path.Combine(outDir, $"slide{exported:D3}.png"), "PNG", width, height);
            }
        }
        finally
        {
            if (presentation is not null)
            {
                presentation.Close();
                Marshal.FinalReleaseComObject(presentation);
            }
            // PowerPoint mógł być już otwarty przez użytkownika — zamykamy go tylko, gdy nic w nim nie zostało.
            try
            {
                if ((int)app.Presentations.Count == 0)
                    app.Quit();
            }
            catch (COMException)
            {
            }
            Marshal.FinalReleaseComObject(app);
        }
    }
}
