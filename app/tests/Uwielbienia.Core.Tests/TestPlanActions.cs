using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

/// <summary>Czynności na planie do testów: własne pieśni i kopie prezentacji w podanym folderze tymczasowym.</summary>
internal static class TestPlanActions
{
    public static PlanActions Create(ActivePlan plan, LiveSession session, ISongLibrary library, string folder)
    {
        var local = Path.Combine(folder, "teksty");
        Directory.CreateDirectory(local);
        var exporter = new NoExporter();
        var slideshows = new SlideshowLibrary(Path.Combine(folder, "prezentacje"), exporter, exporter, new ImmediateDispatcher());
        return new PlanActions(plan, session, session, library, new LocalSongEditor(local, library), slideshows);
    }

    private sealed class NoExporter : ISlideExporter, ISlideExportVersion
    {
        public string Name => "brak";

        public bool IsAvailable => false;

        public string Current => "v1";

        public bool IsOutdated(string folder, string marker) => false;

        public Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress) => Task.CompletedTask;
    }
}
