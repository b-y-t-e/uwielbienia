using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Link;

// Panel: drugi komputer albo tablet z planami, planem, pieśnią i ekranem (app/web/panel). Tylko urządzenie
// z uprawnieniem „Panel” (RemoteCaller.IsPanel); bez niego polecenia są odrzucane.
//
// host → panel (notify):  wszystko co telefon ("state", "plan", "video", "kiosk", "device") oraz
//                         {"type":"plans", ...PlansMessage} — lista planów się zmieniła (nowy, zmieniony, usunięty, otwarty)
// panel → host (request): {"cmd": ...}, odpowiedź CommandReply
//   plany:   "plans" → plans | "planOpen" (planId) | "planCreate" (name, date?, kind: event|template)
//            | "planRename" (planId, name, date?) | "planCopyToToday" (planId) | "planSaveAsTemplate" (planId)
//            | "planDelete" (planId; nie otwarty)
//   plan:    "planAdd" (songId, index? — brak = na koniec) | "planMove" (itemId, index) | "planRemove" (itemId)
//            | "planDuplicate" (itemId) | "showItem" (itemId, slide?) — wspólne z telefonem
//   pozycja: "item" (itemId) → item | "itemLayout" (itemId, layout: [{code, sung}]) → item
//   ekran:   "next" | "prev" | "goto" (slide) | "blank" | "nextItem" | "prevItem" | "video*" — jak telefon
//   pieśni:  "search" (query) — jak telefon
//   obrazy:  "image" (key, width?) — jak telefon (miniatury slajdów: ItemMessage.Images)

/// <summary>Wszystkie plany: wydarzenia od najnowszych, szablony alfabetycznie.</summary>
public sealed record PlansMessage(IReadOnlyList<PlanSummaryMessage> Events, IReadOnlyList<PlanSummaryMessage> Templates, Guid? OpenId)
{
    public string Type => "plans";

    public static PlansMessage From(PlanLibrary plans, Guid? openId) => new(
        plans.List(PlanKind.Event).Select(p => PlanSummaryMessage.From(p, openId)).ToList(),
        plans.List(PlanKind.Template).Select(p => PlanSummaryMessage.From(p, openId)).ToList(),
        openId);
}

/// <param name="Kind"><c>event</c> albo <c>template</c>.</param>
/// <param name="Count">Liczba pozycji planu.</param>
public sealed record PlanSummaryMessage(Guid Id, string Name, DateOnly? Date, string Kind, int Count, bool IsOpen)
{
    public static PlanSummaryMessage From(Plan plan, Guid? openId) =>
        new(plan.Id, plan.Name, plan.Date, PlanMessage.KindName(plan.Kind), plan.Items.Count, plan.Id == openId);
}

/// <summary>Część w układzie pieśni: kod sekcji i czy jest śpiewana.</summary>
public sealed record LayoutEntryMessage(string Code, bool Sung = true);

/// <summary>Wystąpienie części w układzie z wersami (z akordami).</summary>
public sealed record PartMessage(string Code, string Name, bool Sung, IReadOnlyList<LineMessage> Lines);

/// <summary>Część pieśni, której nie ma w układzie („Przywróć usuniętą część”).</summary>
public sealed record SectionMessage(string Code, string Name);

/// <summary>
/// Pozycja planu w kolumnie „Pieśń” panelu. Pieśń i tekst: części w układzie planu (<see cref="Parts"/>), części
/// usunięte (<see cref="Removed"/>); inne rodzaje — tytuł, uwaga i liczba slajdów.
/// </summary>
/// <param name="Kind">Jak <see cref="PlanItemMessage.Kind"/>.</param>
/// <param name="CanReorder">Kolejność i powtórzenia części ustala się tylko dla pieśni; tekst ma kolejność z edytora.</param>
/// <param name="Images">Klucze obrazów slajdów (prezentacja, obraz) do polecenia <c>image</c>; <c>null</c> = pozycja bez obrazów.</param>
/// <param name="Origin"><c>local</c> (własna), <c>modified</c> (zmieniona na tym komputerze) albo <c>null</c>.</param>
public sealed record ItemMessage(
    Guid ItemId,
    string Kind,
    string Title,
    int? Number,
    string? Note,
    int SlideCount,
    string? Category = null,
    string? Origin = null,
    bool CanReorder = false,
    IReadOnlyList<PartMessage>? Parts = null,
    IReadOnlyList<SectionMessage>? Removed = null,
    IReadOnlyList<string?>? Images = null)
{
    public static ItemMessage? From(PlanItem item, PlanItemInfo info, LiveItem? live, ISongLibrary library, ISlideBuilder slides,
        IRemoteImageKeys images)
    {
        var message = new ItemMessage(item.Id, PlanMessage.KindName(info.Kind), info.Title, info.Number,
            info.Note ?? (live is { Slides.Count: 0 } ? live.Note : null), live?.Slides.Count ?? 0,
            Images: live?.Slides.Any(s => s is ImageSlide) == true ? live.Slides.Select(images.KeyOf).ToList() : null);
        if (item is not SongPlanItem songItem || library.Find(songItem.SongId) is not { } song)
            return message;
        var layout = songItem.LayoutFor(song);
        return message with
        {
            Category = song.Category,
            Origin = song.Origin switch
            {
                SongOrigin.Local => "local",
                SongOrigin.Modified => "modified",
                _ => null,
            },
            CanReorder = !song.IsText,
            Parts = layout.Select(entry => SongPart.For(slides, song, entry))
                .Select(p => new PartMessage(p.Code, p.Name, p.Sung, StateMessage.LinesOf(p.Lines)))
                .ToList(),
            Removed = song.Sections.Where(s => layout.All(e => e.Code != s.Code))
                .Select(s => new SectionMessage(s.Code, s.Name)).ToList(),
        };
    }
}
