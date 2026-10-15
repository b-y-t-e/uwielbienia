using System.Text.Json;
using System.Text.RegularExpressions;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.Services;

/// <summary>
/// Slajdy przez DeckCraft (<c>deckcraft-cli</c>): <c>info</c> podaje rozmiar slajdu i slajdy ukryte, <c>render --all</c>
/// rysuje wszystkie slajdy do PNG (12 slajdów w ułamek sekundy, bez okna). Ukryte pomijamy jak w PowerPoincie.
/// </summary>
public sealed partial class DeckCraftExporter : ISlideExporter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly Lazy<string?> _cli = new(() => ExternalTool.Find(
        [ExternalTool.ProgramFiles("DeckCraft", "deckcraft-cli.exe")], "deckcraft-cli"));

    public string Name => "DeckCraft";

    public bool IsAvailable => _cli.Value is not null;

    public async Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
    {
        var cli = _cli.Value ?? throw new InvalidOperationException("Brak programu DeckCraft");
        progress.Report("Slajdy (DeckCraft)…");
        using var info = JsonDocument.Parse(await ExternalTool.RunAsync(cli, ["info", presentation], Timeout).ConfigureAwait(false));
        var slideWidth = info.RootElement.GetProperty("slideSize")[0].GetDouble();
        var hidden = info.RootElement.GetProperty("slides").EnumerateArray()
            .Select(s => s.TryGetProperty("hidden", out var h) && h.GetBoolean())
            .ToList();

        var rendered = Path.Combine(outDir, "deckcraft");
        Directory.CreateDirectory(rendered);
        try
        {
            var scale = (width / slideWidth).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            await ExternalTool.RunAsync(cli, ["render", presentation, "--all", "--scale", scale, rendered], Timeout).ConfigureAwait(false);

            // slide-01.png, slide-02.png… → slide001.png… bez ukrytych
            var exported = 0;
            foreach (var (file, number) in Directory.EnumerateFiles(rendered, "*.png")
                         .Select(f => (f, SlideNumber().Match(Path.GetFileName(f))))
                         .Where(x => x.Item2.Success)
                         .Select(x => (x.f, int.Parse(x.Item2.Value, System.Globalization.CultureInfo.InvariantCulture)))
                         .OrderBy(x => x.Item2))
            {
                if (number - 1 < hidden.Count && hidden[number - 1])
                    continue;
                exported++;
                File.Move(file, Path.Combine(outDir, $"slide{exported:D3}.png"));
            }
        }
        finally
        {
            Directory.Delete(rendered, recursive: true);
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex SlideNumber();
}
