using System.Text;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.Core.Tests;

/// <summary>Wybór programu do eksportu slajdów i licencja Office.</summary>
public sealed class SlideExporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("uwielbienia-export-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Exporter(string name, bool available, bool fails = false) : ISlideExporter
    {
        public int Calls { get; private set; }

        public string Name => name;

        public bool IsAvailable => available;

        public Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
        {
            Calls++;
            File.WriteAllText(Path.Combine(outDir, $"{name}.png"), "");
            return fails ? throw new InvalidOperationException($"{name} zawiódł") : Task.CompletedTask;
        }
    }

    private static readonly IProgress<string> NoProgress = new Progress<string>();

    [Fact]
    public async Task First_available_exporter_is_used()
    {
        var unlicensed = new Exporter("PowerPoint", available: false);
        var deckCraft = new Exporter("DeckCraft", available: true);
        var libreOffice = new Exporter("LibreOffice", available: true);

        await new SlideExporterChain([unlicensed, deckCraft, libreOffice]).ExportAsync("a.pptx", _dir, 1920, NoProgress);

        Assert.Equal((0, 1, 0), (unlicensed.Calls, deckCraft.Calls, libreOffice.Calls));
        Assert.Equal(["DeckCraft.png"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Failed_exporter_falls_back_to_the_next_one_with_a_clean_folder()
    {
        var chain = new SlideExporterChain([new Exporter("PowerPoint", true, fails: true), new Exporter("LibreOffice", true)]);

        await chain.ExportAsync("a.pptx", _dir, 1920, NoProgress);

        Assert.Equal(["LibreOffice.png"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Without_any_program_the_message_says_what_to_install()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SlideExporterChain([new Exporter("PowerPoint", false)]).ExportAsync("a.pptx", _dir, 1920, NoProgress));
        Assert.Equal(SlideExporterChain.NoExporter, error.Message);
    }

    // plik licencji jak w %LOCALAPPDATA%\Microsoft\Office\Licenses\5: UTF-16 bez BOM, License = Base64(JSON)
    private static byte[] LicenseFile(string? expiresOn)
    {
        var inner = expiresOn is null
            ? """{"LicenseType":"Perpetual","Status":"Provisioned"}"""
            : $$"""{"LicenseType":"Subscription","Status":"Provisioned","ExpiresOn":"{{expiresOn}}"}""";
        var outer = $$"""{"License":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes(inner))}}","Certificate":"x"}""";
        return Encoding.Unicode.GetBytes(outer);
    }

    private static readonly DateTimeOffset Today = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-11-02T00:00:00Z", true)]
    [InlineData("2026-10-01T00:00:00Z", false)]
    [InlineData(null, true)]
    public void Office_license_expiry_is_read_from_the_license_file(string? expiresOn, bool active) =>
        Assert.Equal(active, OfficeLicense.IsActive([LicenseFile(expiresOn)], Today));

    [Fact]
    public void One_valid_license_is_enough_and_no_files_mean_unknown_so_allowed()
    {
        Assert.True(OfficeLicense.IsActive([LicenseFile("2026-01-01T00:00:00Z"), LicenseFile("2027-01-01T00:00:00Z")], Today));
        Assert.True(OfficeLicense.IsActive([], Today));
        Assert.True(OfficeLicense.IsActive([Encoding.UTF8.GetBytes("nieczytelny")], Today));
    }
}
