using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.Core.Kiosks;

/// <summary>
/// Rodzaj pozycji „kiosk”: jeden <see cref="KioskSlide"/> z elementów — obrazów i prezentacji (każdy slajd prezentacji
/// to osobny element; przygotowuje je <see cref="ISlideshowLibrary"/>) oraz napisów.
/// </summary>
public sealed class KioskPlanItemType : IPlanItemType
{
    public const string EmptyNote = "Pusty — dodaj obrazy lub napisy";

    private readonly ISlideshowLibrary _library;

    public KioskPlanItemType(ISlideshowLibrary library)
    {
        _library = library;
        library.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public bool Handles(PlanItem item) => item is KioskPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is KioskPlanItem kiosk ? new PlanItemInfo(kiosk.Title, null, PlanItemKind.Kiosk, Note(kiosk, _library.Peek)) : null;

    public LiveItem? Present(PlanItem item)
    {
        if (item is not KioskPlanItem kiosk)
            return null;
        var frames = Frames(kiosk, _library.Load);
        IReadOnlyList<Slide> slides = frames.Count > 0 ? [new KioskSlide(frames, TimeSpan.FromSeconds(Seconds(kiosk)), kiosk.ChosenTransitions, kiosk.SlowZoom)] : [];
        return new LiveItem(item.Id, $"kiosk:{item.Id}", null, kiosk.Title, slides, ShowTitle: false, Note(kiosk, _library.Peek));
    }

    public static int Seconds(KioskPlanItem kiosk) => Math.Clamp(kiosk.Seconds, KioskPlanItem.MinSeconds, KioskPlanItem.MaxSeconds);

    /// <summary>Elementy w kolejności pokazu; prezentacja bez gotowych slajdów jeszcze nic nie wnosi.</summary>
    public static List<KioskFrame> Frames(KioskPlanItem kiosk, Func<string, SlideshowState> load)
    {
        var frames = new List<KioskFrame>();
        foreach (var entry in kiosk.Entries)
        {
            var title = Clean(entry.Title);
            var text = Clean(entry.Text);
            if (entry.Folder is { } folder)
                frames.AddRange(load(folder).Slides.Select(image => new KioskFrame(image, title, text)));
            else if (entry.HasText)
                frames.Add(new KioskFrame(null, title, text));
        }
        return frames;
    }

    /// <summary>Pusty kiosk albo obrazy, których jeszcze (lub już) nie ma — uwaga w planie.</summary>
    private static string? Note(KioskPlanItem kiosk, Func<string, SlideshowState> peek)
    {
        if (!kiosk.Entries.Any(e => e.Folder is not null || e.HasText))
            return EmptyNote;
        return kiosk.Entries.Where(e => e.Folder is not null).Select(e => peek(e.Folder!).Note).FirstOrDefault(n => n is not null);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ReplaceLineEndings("\n");
}
