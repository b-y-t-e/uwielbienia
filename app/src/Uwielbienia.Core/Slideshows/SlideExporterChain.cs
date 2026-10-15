namespace Uwielbienia.Core.Slideshows;

/// <summary>
/// Kilka sposobów eksportu slajdów w kolejności (np. PowerPoint z licencją → DeckCraft → LibreOffice):
/// używa pierwszego dostępnego, a gdy ten zawiedzie — następnego. Błąd tylko wtedy, gdy zawiodły wszystkie.
/// </summary>
public sealed class SlideExporterChain(IReadOnlyList<ISlideExporter> exporters) : ISlideExporter
{
    public const string NoExporter =
        "Do prezentacji potrzebny jest PowerPoint, DeckCraft albo LibreOffice. Można też dodać slajdy zapisane jako obrazy.";

    public string Name => string.Join(", ", exporters.Select(e => e.Name));

    public bool IsAvailable => exporters.Any(e => e.IsAvailable);

    public async Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
    {
        Exception? last = null;
        foreach (var exporter in exporters.Where(e => e.IsAvailable))
        {
            try
            {
                await exporter.ExportAsync(presentation, outDir, width, progress).ConfigureAwait(false);
                if (Directory.EnumerateFiles(outDir).Any())
                    return;
                last = new InvalidOperationException($"{exporter.Name}: brak slajdów");
            }
            catch (Exception ex)
            {
                last = ex;
            }
            // następny sposób zaczyna od pustego folderu
            foreach (var file in Directory.EnumerateFiles(outDir))
                File.Delete(file);
        }
        throw last ?? new InvalidOperationException(NoExporter);
    }
}
