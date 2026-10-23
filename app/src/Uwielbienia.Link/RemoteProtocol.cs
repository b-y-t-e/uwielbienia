using System.Text.Json;
using System.Text.Json.Serialization;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.Link;

// Protokół między aplikacją (host) a stroną www (pilot / ekran). Wszystko w JSON, camelCase.
//
// host → strona (notify):   {"type":"state", ...StateMessage} | {"type":"plan", ...PlanMessage} | {"type":"video", ...VideoMessage}
//                           | {"type":"kiosk", ...KioskMessage} (bieżący element kiosku na ekranie — zmienia się sam)
//                           | {"type":"device", ...DeviceMessage} (operator zmienił uprawnienia tego urządzenia)
// strona → host (request):  {"cmd":"hello"|"next"|"prev"|"blank"|"goto"|"showItem"|"showSong"|"search"
//                                  |"videoToggle"|"videoSkip" (seconds: ±15)
//                                  |"videoOffset" (milliseconds: ±100 — obraz później / wcześniej względem dźwięku)
//                                  |"addNext" (songId — pieśń zaraz za tą na ekranie; tylko z uprawnieniem)
//                                  |"undoAdd" (itemId — cofnięcie własnego dodania, póki pieśni nie ma na ekranie)
//                                  |"image" (key — z StateMessage.Image / NextImage albo ItemMessage.Images, width?: 320 | 640 | 1280)
//                                  → image (data URL JPEG), ...}
//                           odpowiedź: CommandReply
//
// Panel (drugi komputer / tablet, uprawnienie „Panel”) — dodatkowo polecenia i wiadomości z PanelProtocol.cs.

public sealed record RemoteCommand(
    string Cmd,
    int? Slide = null,
    Guid? ItemId = null,
    string? SongId = null,
    string? Query = null,
    int? Seconds = null,
    int? Milliseconds = null,
    Guid? PlanId = null,
    string? Name = null,
    DateOnly? Date = null,
    string? Kind = null,
    int? Index = null,
    IReadOnlyList<LayoutEntryMessage>? Layout = null,
    string? Key = null,
    int? Width = null);

public sealed record CommandReply(
    bool Ok,
    string? Error = null,
    StateMessage? State = null,
    PlanMessage? Plan = null,
    IReadOnlyList<SongHit>? Results = null,
    VideoMessage? Video = null,
    DeviceMessage? Device = null,
    PlanItemMessage? Added = null,
    KioskMessage? Kiosk = null,
    PlansMessage? Plans = null,
    ItemMessage? Item = null,
    string? Image = null);

/// <summary>
/// Co wolno temu urządzeniu: z uprawnieniem telefon pokazuje „+” (dodaj pieśń jako następną); panel bez
/// <see cref="IsPanel"/> prosi operatora o włączenie „Panel” w oknie „Urządzenia”.
/// </summary>
public sealed record DeviceMessage(bool CanAddSongs, bool IsPanel = false)
{
    public string Type => "device";

    public static DeviceMessage From(RemoteCaller caller) => new(caller.CanAddSongs, caller.IsPanel);
}

public sealed record StateMessage(
    long Version,
    string? SongId,
    Guid? ItemId,
    int? Number,
    string? Title,
    int SlideIndex,
    int SlideCount,
    string? Label,
    IReadOnlyList<LineMessage> Lines,
    IReadOnlyList<string> SlideLabels,
    bool IsBlank,
    string? Next,
    bool ShowTitle = false,
    IReadOnlyList<LineMessage>? NextLines = null,
    bool IsVideo = false,
    bool IsKiosk = false,
    string? Image = null,
    string? NextImage = null)
{
    /// <summary>Uruchomienie aplikacji, z którego pochodzi stan — wersje porównuje się tylko w jednym uruchomieniu.</summary>
    public static readonly string CurrentSession = Guid.NewGuid().ToString("N");

    public string Type => "state";

    public string Session => CurrentSession;

    public static StateMessage From(LiveState state, IRemoteImageKeys images) => new(
        state.Version,
        state.Item?.ContentId,
        state.Item?.PlanItemId,
        state.Item?.Number,
        state.Item?.Title,
        state.SlideIndex,
        state.Item?.Slides.Count ?? 0,
        state.Slide?.Label,
        LinesOf(state.Slide),
        state.Item?.Slides.Select(s => s.Label).ToList() ?? [],
        state.IsBlank,
        state.Next,
        // tylko slajd z tekstem (także sam tytuł bez wersów) — nie obraz ani film
        state.Item is { ShowTitle: true } && state.SlideIndex == 0 && state.Slide?.GetType() == typeof(Slide),
        // następny slajd tej samej pieśni — muzyk widzi, co nadchodzi (inna pieśń: tylko tytuł w Next)
        state.NextSlide is { } next ? LinesOf(next) : null,
        state.Slide is VideoSlide,
        state.Slide is KioskSlide,
        // obraz slajdu (prezentacja) i następnego — strona pobiera je poleceniem „image”, gdy je pokazuje
        images.KeyOf(state.Slide),
        images.KeyOf(state.NextSlide));

    private static List<LineMessage> LinesOf(Slide? slide) => LinesOf(slide?.Lines ?? []);

    public static List<LineMessage> LinesOf(IEnumerable<SlideLine> lines) =>
        lines.Select(l => new LineMessage(l.Text, l.Repeat, l.IsChorus, l.StartsPart, l.TitleLength, l.Chords)).ToList();
}

/// <param name="Chorus">Refren dołączony do zwrotki (kursywa).</param>
/// <param name="PartStart">Początek dołączonej części (odstęp nad wersem).</param>
/// <param name="TitleLength">Ile pierwszych znaków wersu powtarza tytuł pieśni (kolor tytułu).</param>
/// <param name="Chords">Akordy wersu (telefon muzyka), jak w kolumnie „Na ekranie”.</param>
public sealed record LineMessage(
    string Text,
    int Repeat,
    bool Chorus = false,
    bool PartStart = false,
    int TitleLength = 0,
    string? Chords = null);

/// <summary>Film na ekranie: telefon pokazuje czas, pasek postępu, pauzę, ±15 s i przesunięcie obrazu względem dźwięku
/// (<see cref="PictureOffsetMs"/>, + = obraz później). Wysyłane przy każdej zmianie stanu odtwarzacza, czas — najwyżej
/// raz na sekundę.</summary>
public sealed record VideoMessage(
    bool IsLoaded, bool IsPlaying, double Position, double Duration, string? Error, int PictureOffsetMs = 0)
{
    public string Type => "video";

    public static VideoMessage From(IVideoPlayer player) => new(
        player.Status.File is not null, player.Status.IsPlaying, Math.Round(player.Status.Position.TotalSeconds, 1),
        Math.Round(player.Status.Duration.TotalSeconds, 1), player.Status.Error,
        player.PictureOffsetMs());
}

/// <summary>Otwarty plan: pozycje w kolejności planu (także te bez slajdów — np. prezentacja w trakcie eksportu).</summary>
/// <param name="Id">Plan; <c>null</c> = żaden nie jest otwarty.</param>
/// <param name="PlanKind"><c>event</c> albo <c>template</c>.</param>
public sealed record PlanMessage(
    string? Name,
    IReadOnlyList<PlanItemMessage> Items,
    Guid? Id = null,
    DateOnly? Date = null,
    string? PlanKind = null)
{
    public string Type => "plan";

    public static PlanMessage From(Plan? plan, IReadOnlyList<LiveItem> playlist, IPlanItemTypes itemTypes) =>
        new(plan?.Name,
            (plan?.Items ?? []).Select(item => ItemOf(item, playlist, itemTypes)).OfType<PlanItemMessage>().ToList(),
            plan?.Id, plan?.Date, plan is null ? null : KindName(plan.Kind));

    public static string KindName(PlanKind kind) => kind == Core.Plans.PlanKind.Template ? "template" : "event";

    /// <summary>
    /// Znacznik na liście jak w aplikacji — z opisu pozycji (<see cref="PlanItemKind"/>): <c>song</c>, <c>text</c>,
    /// <c>presentation</c>, <c>video</c>, <c>kiosk</c>, <c>app</c>; nowy rodzaj pozycji działa bez zmian tutaj.
    /// </summary>
    public static string KindName(PlanItemKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    private static PlanItemMessage? ItemOf(PlanItem item, IReadOnlyList<LiveItem> playlist, IPlanItemTypes itemTypes)
    {
        if (itemTypes.Describe(item) is not { } info)
            return null;
        var live = playlist.FirstOrDefault(i => i.PlanItemId == item.Id);
        return new PlanItemMessage(item.Id, live?.ContentId ?? (item as SongPlanItem)?.SongId ?? "", info.Number, info.Title,
            KindName(info.Kind), info.Note ?? (live is { Slides.Count: 0 } ? live.Note : null), live?.Slides.Count ?? 0);
    }
}

/// <param name="Note">Uwaga pod tytułem jak w planie aplikacji (np. „Przygotowywanie slajdów…”, brak pliku).</param>
/// <param name="SlideCount">Slajdy do pokazania; 0 = pozycji nie da się teraz pokazać.</param>
public sealed record PlanItemMessage(
    Guid Id, string SongId, int? Number, string Title, string Kind = "song", string? Note = null, int SlideCount = 0);

public sealed record SongHit(string SongId, int? Number, string Title, string FirstLine, bool IsText = false);

public static class RemoteJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
}
